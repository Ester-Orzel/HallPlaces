# HallPlaces – מסמך ארכיטקטורה

מערכת להזמנת מקומות ישיבה באירועים. מנהל מגדיר אולמות ואירועים, לקוחות מזמינים מושבים.
**המשאב המוגבל:** מושב מסוים באירוע מסוים. שני לקוחות שמנסים להזמין אותו בו-זמנית – רק אחד מצליח, השני מקבל 409.

---

## 1. שכבות ותלויות

```
HallPlaces.API      Controllers, Middleware, Program.cs        → Service, Core (+ Data רק לרישום DI)
HallPlaces.Service  לוגיקה עסקית                               → Core
HallPlaces.Data     DbContext, Repositories, UnitOfWork, Migrations → Core
HallPlaces.Core     Entities, Enums, Interfaces, DTOs, Exceptions  → (כלום)
HallPlaces.Tests    xUnit + Moq                                 → Service, Core, Data
```

| שכבה | תפקיד | אסור לה |
|---|---|---|
| API | לקבל HTTP, ולידציית פורמט (Data Annotations), לתרגם תוצאה לסטטוס | לגעת ב-DbContext, להכיל חוקים עסקיים |
| Service | חוקים עסקיים, החלטות, יצירת מושבים, טיפול בהתנגשות | להכיר HTTP או EF Core |
| Data | שליפה ושמירה, תרגום חריגות EF לחריגות של Core | להחליט חוקים עסקיים |
| Core | הגדרות משותפות | להפנות לכל פרויקט או חבילה חיצונית |

**מבחן:** אם מחליפים את ה-API ב-Console – קוד שעדיין נחוץ שייך ל-Service.

---

## 2. ישויות

| ישות | שדות | הערות |
|---|---|---|
| **Hall** | Id, Name, Address, `List<int> RowSeatCounts`, Events | **תבנית** בלבד. `[10,12,14]` = שורה 1: 10 מושבים וכו'. נשמר כעמודה אחת (מערך integer ב-PostgreSQL) |
| **Event** | Id, Title, Description?, StartsAt (UTC), CreatedAt, HallId, Hall, Seats, Categories | |
| **Seat** | Id, EventId, RowNumber, SeatNumber, Price, Status, **Version (Guid)**, Bookings | **המשאב המוגבל**. עותק קפוא של מבנה האולם, נוצר ביצירת האירוע |
| **Booking** | Id, UserId, SeatId, Status, CreatedAt, CancelledAt? | ביטול = שינוי סטטוס, לא מחיקה |
| **User** | Id, FullName, Email, PasswordHash, Role, CreatedAt | רק hash של הסיסמה |
| **Category** | Id, Name, Events | |

**Enums:** SeatStatus (Available, Booked) · BookingStatus (Active, Cancelled) · UserRole (Customer, Admin) · EventSortBy (Date, Title)

### קשרים
```
Hall 1 ──── * Event * ──── * Category      ← many-to-many (טבלת CategoryEvent)
               1
               │
               * Seat 1 ──── * Booking * ──── 1 User
```
- Hall → Event: one-to-many, **Restrict** (אי אפשר למחוק אולם עם אירועים)
- Event → Seat: one-to-many, **Cascade** (מחיקת אירוע בלי הזמנות מוחקת את מושביו)
- Seat → Booking: one-to-many, **Restrict**
- User → Booking: one-to-many, **Restrict**

---

## 3. החלטות תכנון

1. **מתי טבלה ומתי מערך?** טבלה – אם מצביעים עליו, משנים אותו בנפרד, או מחפשים לפיו.
   מושב עונה על שלושתם → טבלה. שורות אולם – על אף אחד → `List<int>` בתוך Hall.
2. **מושבים שייכים לאירוע, לא לאולם.** אותו כיסא פיזי הוא משאב נפרד בכל אירוע (פנוי באחד, תפוס בשני).
   האולם הוא שרטוט משותף; המושבים הם העתק לכל ערב.
3. **גודל היחידה הנעולה = גודל המשאב.** Version על כל מושב, כך שהתנגשות קורית רק כשמתחרים על אותו כיסא בדיוק.
4. **Seat.Status נשמר במכוון** (אף שאפשר להסיק אותו מההזמנות): הזמנה חייבת לעדכן את שורת המושב,
   אחרת Version לא היה מזהה את ההתנגשות. בנוסף – **אינדקס ייחודי מסונן** על Bookings(SeatId) WHERE Status = Active,
   כרשת ביטחון ברמת בסיס הנתונים.
5. **כל נתון במקום אחד.** תאריך האירוע ב-Event בלבד; ההזמנה מגיעה אליו דרך Seat.Event.
6. **IUnitOfWork:** ה-Repositories רק מכינים שינויים; שמירה אחת = טרנזקציה אחת (עדכון מושב + יצירת הזמנה יחד).
7. **ConcurrencyConflictException (ב-Core):** Data תופס `DbUpdateConcurrencyException` של EF וזורק את שלנו,
   כדי ש-Service לא יצטרך להכיר את EF Core.
8. **קריאה מול עדכון:** מתודות קריאה – AsNoTracking. מתודות `GetFor...` / `GetById` לעדכון – Tracked.
9. **הזמנה אחת = מושב אחד.** פישוט מכוון.
10. **תאריכים ב-UTC** בכל המערכת; המרה לשעון מקומי רק בתצוגה.
11. **בלי כפל קוד.** פעולות זהות לכל הישויות (AddAsync, Remove) מוגדרות פעם אחת ב-`IRepository<T>`
    וממומשות פעם אחת במחלקת בסיס `Repository<T>` ב-Data. כל Repository ספציפי מוסיף רק את השאילתות שלו.
    אותו עיקרון בכל השכבות: פונקציה שכבר קיימת – משתמשים בה, לא כותבים שוב.
12. **Fluent API ב-`OnModelCreating`** של AppDbContext (כמו בחומר הקורס), בלי מחלקות Configuration נפרדות.
13. **רישום ה-DI גלוי ב-`Program.cs`** (AddDbContext, AddScoped...), כנדרש: "הרישום נעשה ב-Program.cs".
14. **Service מחזיר `Result<T>`** (כמו בדוגמת ההוראות); ה-Controller מתרגם אותו לסטטוס HTTP.
    שגיאות לא צפויות נתפסות ב-Middleware הגלובלי.
15. **חריגה אחת מדוגמת ההוראות, במכוון:** ההוראות תופסות `DbUpdateConcurrencyException` ישירות ב-Service.
    אצלנו Data תופס אותה וזורק `ConcurrencyConflictException` של Core, כדי ש-Service לא יצטרך להכיר את EF Core.
    אם המורה מעדיפה את הדרך הישירה – אפשר לשנות (דורש הפניה של Service ל-EF Core).

---

## 4. חוקים עסקיים (ב-Service) וסטטוסים

| מצב | סטטוס |
|---|---|
| מושב תפוס, או נתפס במקביל (concurrency) | **409** |
| הזמנה לאירוע שכבר התחיל | 400 |
| ביטול הזמנה של משתמש אחר | 403 |
| ביטול אחרי תחילת האירוע / ביטול הזמנה שכבר בוטלה | 400 |
| מחיקת אולם עם אירועים / אירוע עם הזמנות | 409 |
| אימייל קיים בהרשמה / שם קטגוריה קיים | 409 |
| משאב לא קיים | 404 |
| לא מחובר | 401 · מחובר בלי הרשאה | 403 |

### זרימת הזמנה
```
POST /api/bookings { seatId }
  Service: seat = GetForBookingAsync(seatId)            (Tracked, כולל Version)
           אם לא קיים → 404 · אירוע עבר → 400 · Status = Booked → 409
           seat.Status = Booked ; Add(new Booking)
           UnitOfWork.SaveChangesAsync
             AppDbContext מחליף Version ל-Guid חדש
             SQL: UPDATE "Seats" SET "Status"=1, "Version"=@new WHERE "Id"=@id AND "Version"=@old
             0 שורות עודכנו → DbUpdateConcurrencyException
               → Data זורק ConcurrencyConflictException
               → Service מחזיר Conflict + לוג Warning
               → API מחזיר 409 "המושב נתפס על ידי משתמש אחר"
```

---

## 5. Roles והרשאות

| פעולה | אורח | Customer | Admin |
|---|---|---|---|
| הרשמה / התחברות | ✔ | | |
| רשימת אירועים, פרטי אירוע, מפת מושבים, קטגוריות | ✔ | ✔ | ✔ |
| הזמנת מושב, ההזמנות שלי, ביטול הזמנה שלי | | ✔ | ✔ |
| ניהול אולמות, אירועים, קטגוריות | | | ✔ |
| צפייה בכל ההזמנות של אירוע | | | ✔ |

---

## 6. Endpoints מתוכננים

| Method | נתיב | הרשאה |
|---|---|---|
| POST | /api/auth/register · /api/auth/login | – |
| GET | /api/events?page&pageSize&search&categoryId&upcomingOnly&sortBy&descending | – |
| GET | /api/events/{id} · /api/events/{id}/seats | – |
| POST · PUT · DELETE | /api/events · /api/events/{id} | Admin |
| GET | /api/events/{id}/bookings | Admin |
| GET · POST · PUT · DELETE | /api/halls · /api/halls/{id} | GET – כולם, השאר Admin |
| GET · POST · PUT · DELETE | /api/categories · /api/categories/{id} | GET – כולם, השאר Admin |
| POST | /api/bookings | Customer |
| GET | /api/bookings/me | Customer |
| DELETE | /api/bookings/{id} (ביטול) | Customer – רק שלו |

---

## 7. בסיס נתונים ו-Concurrency token

- **PostgreSQL** (נדרש כדי להעלות ל-Render). מקומית: PostgreSQL מותקן במחשב. בענן: Render PostgreSQL.
- **Concurrency token:** `[ConcurrencyCheck] Guid Version` על Seat (הדרך השנייה בחלק ח' של ההוראות).
  מוחלף אוטומטית ב-`AppDbContext.SaveChangesAsync` – נכתב פעם אחת, אף Service לא צריך לזכור.
  נבחר על פני xmin כי הוא עובד בכל בסיס נתונים, כך שאפשר לבדוק אותו גם בבדיקות אוטומטיות.
- **Connection string** כולל סיסמה → נשמר ב-**User Secrets**, לא ב-repo.
- **קבצי הגדרות לפי סביבה:** `appsettings.json` (משותף), `appsettings.Development.json` (פיתוח: לוגים ברמת Debug ושאילתות SQL),
  `appsettings.Production.json` (ייצור). בקבצים אין סודות. סודות: בפיתוח User Secrets, ב-Render משתני סביבה.
- **כל התאריכים ב-UTC** – Npgsql זורק חריגה על DateTime שאינו UTC.

## 8. העלאה ל-Render (חלק ז')

- שרת: Web Service מ-GitHub, באמצעות Dockerfile (Render לא מריץ ‎.NET ישירות).
- לקוח: Static Site.
- בסיס נתונים: Render PostgreSQL. **החינמי פג אחרי 30 יום** – ליצור קרוב להגשה.
- הגדרות (connection string, מפתח JWT) – כמשתני סביבה ב-Render, לא בקוד.

## 9. סדר העבודה

1. ✔ מבנה ה-solution (4 פרויקטים + תלויות)
2. ✔ Core: ישויות, Enums, ממשקי Repositories, Common, Exceptions
3. ✔ Data: PostgreSQL, AppDbContext + Fluent API, Migration ראשון. נשאר: `Update-Database`
4. Data: Repositories + UnitOfWork (AsNoTracking, pagination ב-DB, Include/ThenInclude, תרגום חריגת concurrency)
5. Core: DTOs (request/response נפרדים), `Result<T>`, ממשקי Services · Service: לוגיקה עסקית + AutoMapper Profiles
6. API: Controllers (REST, סטטוסים, Location), ולידציה, Swagger
7. Middleware: טיפול גלובלי בשגיאות (ראשון בצינור), CorrelationId
8. NLog: nlog.config, לוג לכל בקשה עם CorrelationId, Error לחריגה, Warning להתנגשות, בלי נתונים רגישים
9. JWT: הרשמה/התחברות, הצפנת סיסמה, claims, 2 roles, [Authorize], מפתח ב-User Secrets, IOptions
10. Seed + הרצת Migrations אוטומטית בעליית השרת + Migration שני
11. Tests: Service עם Moq (הזמנה תקינה / נדחית) + בדיקת concurrency עם שני DbContext
12. README + בדיקת `.gitignore` + repository של השרת ב-GitHub
13. לקוח React (4 מסכים) + repository נפרד
14. העלאה ל-Render: Dockerfile לשרת, PostgreSQL, Static Site ללקוח, משתני סביבה, CORS
15. בדיקה סופית מול רשימת הדרישות
