# HallPlaces – הזמנת מושבים לאירועים (Server)

מערכת Web API להזמנת מושבים באולמות. מנהל מגדיר אולמות, קטגוריות ואירועים; לקוחות מחוברים בוחרים מושב ומזמינים אותו.
פרויקט גמר בקורס .NET – צד השרת (ASP.NET Core, .NET 10, PostgreSQL, EF Core Code-First).
צד הלקוח (React) נמצא בריפוזיטורי נפרד: `<קישור לריפו של הלקוח>`.

**כתובות באינטרנט (Render):**
- שרת + Swagger: `<קישור לשרת>/swagger`
- לקוח: `<קישור ללקוח>`

> השרת בתוכנית החינמית של Render "ישן" אחרי 15 דקות ללא פעילות. הבקשה הראשונה יכולה לקחת כדקה.

---

## המשאב המוגבל ותחרות עליו

**המשאב המוגבל הוא מושב באירוע.** מושב אחד יכול להיות מוזמן על ידי משתמש אחד בלבד.

**התרחיש:** שני משתמשים לוחצים "הזמן" על אותו מושב באותו רגע. שניהם קוראים את המושב והוא נראה להם פנוי.
בלי הגנה – שניהם היו מצליחים (הזמנה כפולה). אצלנו **בדיוק אחד מצליח, והשני מקבל 409 Conflict** עם הודעה ברורה.

**הפתרון – Optimistic Concurrency:**
- לישות `Seat` יש שדה `Version` (`Guid`) המסומן `[ConcurrencyCheck]`.
- בכל שינוי של מושב, `AppDbContext.SaveChangesAsync` מחליף את ה-`Version` בערך חדש.
- ב-`UPDATE` EF Core מוסיף לתנאי את ה-`Version` שקרא: `UPDATE Seats SET ... WHERE Id = @id AND Version = @versionThatWasRead`.
- מי ששמר ראשון – ה-`Version` שלו תואם והעדכון מצליח. מי ששמר שני – ה-`Version` כבר השתנה, העדכון לא משפיע על אף שורה,
  ו-EF זורק `DbUpdateConcurrencyException`.
- ה-`UnitOfWork` (שכבת Data) תופס אותו ומתרגם ל-`ConcurrencyConflictException` (ב-Core, כדי שה-Service לא יכיר את EF).
  ה-`BookingService` תופס אותה, רושם **Warning** בלוג ומחזיר `409` עם ההודעה: *"Someone else just booked this seat. Please choose another seat."*
- רשת ביטחון נוספת במסד: אינדקס ייחודי מסונן – לכל מושב לכל היותר הזמנה **פעילה** אחת (`WHERE "Status" = 0`).

**למה `Guid Version` ולא `xmin`:** שדה מנוהל ידנית עובד זהה ב-PostgreSQL וב-SQLite (בבדיקות), והלוגיקה גלויה בקוד.

**למה נעילה אופטימית ולא נעילה במסד:** קונפליקט על אותו מושב הוא נדיר, ולכן עדיף לא לנעול שורות. מזהים התנגשות רק ברגע השמירה.

**איפה זה נבדק:** `HallPlaces.Tests/ConcurrencyTests.cs` – שני `DbContext` נפרדים קוראים את אותו מושב, שניהם משנים,
הראשון שומר בהצלחה והשני מקבל `ConcurrencyConflictException`.

---

## ארכיטקטורה

```
HallPlaces.Core      Entities, Enums, DTOs, Interfaces (Repositories / Services / Security), Result, Exceptions. לא מפנה לשום פרויקט.
HallPlaces.Data      AppDbContext (Fluent API), Repositories, UnitOfWork, Migrations. מפנה ל-Core.
HallPlaces.Service   לוגיקה עסקית, AutoMapper, JWT, Seed. מפנה ל-Core.
HallPlaces.API       Controllers, Middleware, Program.cs. מפנה ל-Service ול-Core; מכיר את Data רק לרישום ה-DI.
HallPlaces.Tests     xUnit + Moq.
```

- ה-interfaces ב-Core, המימושים ב-Data וב-Service, הרישום ב-`Program.cs` (DbContext – Scoped).
- ה-Service מחזיר `Result<T>`; ה-Controller מתרגם לסטטוס HTTP (200/201/204/400/401/403/404/409). שגיאות לא צפויות נתפסות ב-Middleware גלובלי.
- ה-Controllers לא מכירים את `DbContext` ולא מכילים לוגיקה.
- פירוט נוסף והחלטות עיצוב: `ARCHITECTURE.md`.

### מודל הנתונים
- `Hall` (אולם – תבנית, `RowSeatCounts` = כמה מושבים בכל שורה) ← 1:N → `Event` (אירוע באולם)
- `Event` ← 1:N → `Seat` (לכל אירוע עותק משלו של המושבים, נוצר מתבנית האולם)
- `Event` ↔ `Category` – **רבים לרבים**
- `Booking` מקשר `User` ל-`Seat`

### תפקידים והרשאות
| פעולה | Anonymous | Customer | Admin |
|---|:-:|:-:|:-:|
| צפייה באירועים, במפת מושבים ובקטגוריות | ✓ | ✓ | ✓ |
| הרשמה / התחברות | ✓ | ✓ | ✓ |
| הזמנת מושב, ההזמנות שלי | | ✓ | ✓ |
| ביטול הזמנה | | רק שלו | כל הזמנה |
| ניהול אולמות, אירועים וקטגוריות; הזמנות של אירוע | | | ✓ |

### Endpoints עיקריים
| Method | Path | הרשאה |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | פתוח |
| GET | `/api/events?page=&pageSize=&search=&categoryId=&sortBy=&descending=` | פתוח (עימוד, סינון ומיון במסד) |
| GET | `/api/events/{id}`, `/api/events/{id}/seats` | פתוח |
| POST/PUT/DELETE | `/api/events`, `/api/events/{id}` | Admin |
| GET/POST | `/api/categories` | GET פתוח, POST Admin |
| GET/POST/PUT/DELETE | `/api/halls` | Admin |
| POST | `/api/bookings` | מחובר (201 / 409 / 404) |
| GET | `/api/bookings/mine` | מחובר |
| DELETE | `/api/bookings/{id}` | מחובר (204) |
| GET | `/api/events/{id}/bookings` | Admin |

---

## הרצה מקומית

### דרישות
- .NET SDK 10
- PostgreSQL (מותקן ורץ מקומית, ברירת מחדל: פורט 5432)
- Visual Studio 2022+ או `dotnet` CLI

### שלבים
1. שכפול הריפו.
2. **סודות** – נשמרים ב-User Secrets (מחוץ לריפו). מתוך תיקיית הפתרון:
   ```
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=HallPlacesDb;Username=postgres;Password=<הסיסמה שלך>" --project HallPlaces.API
   dotnet user-secrets set "Jwt:Key" "<מחרוזת אקראית של 32 תווים לפחות>" --project HallPlaces.API
   ```
3. הרצה: `dotnet run --project HallPlaces.API` (או F5 ב-Visual Studio). Swagger נפתח ב-`/swagger`.

**אין צורך בשום פעולה ידנית במסד הנתונים:** בהפעלה השרת יוצר את המסד, מריץ את כל ה-Migrations ומזין נתוני דמו (Seed).

### משתמשי דמו
| תפקיד | אימייל | סיסמה |
|---|---|---|
| Admin | `admin@hallplaces.com` | `Admin123!` |
| Customer | `dana@hallplaces.com` | `Customer123!` |
| Customer | `ron@hallplaces.com` | `Customer123!` |

(שני לקוחות – כדי להדגים תחרות על אותו מושב.) הנתונים: 2 אולמות, 4 קטגוריות, 3 אירועים עתידיים.

### בדיקת ההתנגשות ידנית ב-Swagger
1. התחברי כ-`dana` (`POST /api/auth/login`), הדביקי את הטוקן בכפתור **Authorize**, והזמיני מושב (`POST /api/bookings`, `{"seatId": 1}`) → `201`.
2. התחברי כ-`ron`, הזמיני את אותו מושב → `409`.

### בדיקות
`dotnet test` (או Test Explorer ב-Visual Studio). בדיקות ה-Service משתמשות ב-Moq; בדיקת ההתנגשות משתמשת ב-SQLite בזיכרון, ולכן אינה דורשת PostgreSQL.

---

## קונפיגורציה, לוגים ואבטחה

- `appsettings.json` (משותף), `appsettings.Development.json`, `appsettings.Production.json`. סדר הטעינה: appsettings ← קובץ הסביבה ← User Secrets (פיתוח בלבד) ← משתני סביבה.
- **אין סודות בקוד או בריפו.** מחרוזת החיבור ומפתח ה-JWT נקראים מ-User Secrets (פיתוח) או ממשתני סביבה (Render: `ConnectionStrings__Default`, `Jwt__Key`).
- **Logging:** NLog (`nlog.config`) – קונסולה וקובץ ב-`logs/`. כל בקשה נרשמת עם `CorrelationId` (גם ב-header `X-Correlation-Id` של התשובה).
  `Error` – חריגה לא צפויה; `Warning` – התנגשות על משאב; לעולם לא נרשמים סיסמאות, טוקנים או גוף בקשות הרשמה/התחברות.
- סיסמאות נשמרות כ-hash (BCrypt). התפקיד נקבע בשרת בלבד: כל נרשם הוא Customer.

## פריסה ב-Render
- השרת: Web Service מ-Docker (`Dockerfile` בתיקיית הפתרון).
- מסד נתונים: PostgreSQL של Render.
- משתני סביבה: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Default`, `Jwt__Key`.
- הלקוח: Static Site בריפו נפרד.
