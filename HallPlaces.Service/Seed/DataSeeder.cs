using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Security;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HallPlaces.Service.Seed;

// Fills an EMPTY database with demo data, so the system works right after the first run.
// Safe to run on every startup: it adds only what is missing.
//
// It uses the SAME Services as the API (no duplicated logic): for example the seats of a seeded
// event are created by EventService, exactly as when an admin creates an event.
//
// The passwords below are DEMO passwords, written in the README. Real secrets never live here.
public class DataSeeder
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryService _categoryService;
    private readonly IHallService _hallService;
    private readonly IEventService _eventService;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICategoryService categoryService,
        IHallService hallService,
        IEventService eventService,
        ILogger<DataSeeder> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _categoryService = categoryService;
        _hallService = hallService;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedUsersAsync(ct);
        await SeedCatalogAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        await AddUserIfMissingAsync("Site Admin", "admin@hallplaces.com", "Admin123!", UserRole.Admin, ct);
        await AddUserIfMissingAsync("Dana Levi", "dana@hallplaces.com", "Customer123!", UserRole.Customer, ct);
        await AddUserIfMissingAsync("Ron Cohen", "ron@hallplaces.com", "Customer123!", UserRole.Customer, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task AddUserIfMissingAsync(string fullName, string email, string password, UserRole role, CancellationToken ct)
    {
        if (await _users.EmailExistsAsync(email, ct))
        {
            return;
        }

        await _users.AddAsync(new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            Role = role
        }, ct);

        _logger.LogInformation("Seeding the demo user {Email} ({Role})", email, role);
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        // Already has halls = the catalog was seeded before (or an admin created his own data).
        if (Unwrap(await _hallService.GetAllAsync(ct)).Count > 0)
        {
            return;
        }

        _logger.LogInformation("Seeding the demo halls, categories and events");

        // Name -> Id, so the events below can refer to a category / hall by its name.
        var categories = new Dictionary<string, int>();
        foreach (var name in new[] { "Torah Classes", "Niggun & Music", "Kids & Family", "Gatherings", "Theater", "Chizuk & Emunah", "Yamim Tovim" })
        {
            categories[name] = await CreateCategoryAsync(name, ct);
        }

        var halls = new Dictionary<string, int>
        {
            ["Heichal HaTorah"] = await CreateHallAsync("Heichal HaTorah", "100 Rabbi Akiva St, Bnei Brak", new List<int> { 10, 12, 12, 14, 14, 16 }, ct),
            ["Beit Simcha Hall"] = await CreateHallAsync("Beit Simcha Hall", "20 Malchei Yisrael St, Jerusalem", new List<int> { 8, 10, 10, 12, 12, 14 }, ct),
            ["Kiryat Sefer Convention Center"] = await CreateHallAsync("Kiryat Sefer Convention Center", "10 Avnei Nezer St, Modiin Illit", new List<int> { 12, 14, 16, 16, 18, 18 }, ct),
            ["Shaarei Chesed Hall"] = await CreateHallAsync("Shaarei Chesed Hall", "12 Nahar HaYarden St, Beit Shemesh", new List<int> { 6, 8, 8, 10 }, ct),
            ["Ohel Yaakov Hall"] = await CreateHallAsync("Ohel Yaakov Hall", "30 Chazon Ish St, Elad", new List<int> { 8, 8, 10, 10, 12 }, ct)
        };

        // One line per event: title, description, in how many days, hour (UTC), hall, BASE seat price, categories.
        // The base price is the price of the back rows; the front rows cost more (EventService.PriceForRow).
        // The events are spread over the next three and a half months, so paging, searching and sorting have something to show.
        var events = new (string Title, string Description, int Days, int HourUtc, string Hall, decimal Price, string[] Categories)[]
        {
            ("Shabbos Niggunim Evening", "Niggunim for the Shabbos table with a live orchestra. Seating is fully separate.", 3, 17, "Heichal HaTorah", 60m, new[] { "Niggun & Music" }),
            ("Hilchos Shabbos In-Depth Shiur", "A shiur klali on the melacha of borer, with a source sheet for every participant.", 4, 18, "Shaarei Chesed Hall", 20m, new[] { "Torah Classes" }),
            ("Yosef and His Brothers: A Musical Play", "A family musical based on Parshas Vayeshev.", 5, 14, "Beit Simcha Hall", 45m, new[] { "Theater", "Kids & Family" }),
            ("Shmiras HaLashon Gathering", "An evening of chizuk on guarding our speech. Seating is fully separate.", 7, 18, "Kiryat Sefer Convention Center", 30m, new[] { "Chizuk & Emunah", "Gatherings" }),
            ("Siyum Masechta Brachos", "A siyum and seudas mitzvah in honor of the learners.", 8, 17, "Heichal HaTorah", 80m, new[] { "Gatherings" }),
            ("Children's Choir: Songs of the Yamim Tovim", "The talmud torah boys' choir sings songs of the chagim.", 10, 15, "Beit Simcha Hall", 35m, new[] { "Niggun & Music", "Kids & Family" }),
            ("Weekly Parsha Shiur", "The parsha of the week through the eyes of the meforshim.", 11, 18, "Shaarei Chesed Hall", 15m, new[] { "Torah Classes" }),
            ("Chassidic Niggunim Concert", "Niggunei dveikus and simcha with a brass orchestra. Seating is fully separate.", 13, 18, "Kiryat Sefer Convention Center", 120m, new[] { "Niggun & Music" }),
            ("Family Night: Magic Show", "A magic show, niggunim and light refreshments for the whole family.", 14, 14, "Beit Simcha Hall", 40m, new[] { "Kids & Family" }),
            ("Evening of Tehillim and Tefillah", "Tehillim said together, followed by words of chizuk.", 16, 17, "Heichal HaTorah", 25m, new[] { "Chizuk & Emunah" }),
            ("The Wisdom of Shlomo HaMelech", "A play based on the midrashim about the wisdom of Shlomo HaMelech.", 18, 15, "Beit Simcha Hall", 50m, new[] { "Theater", "Kids & Family" }),
            ("Parents' Conference: Chinuch with Love", "Talks and a questions-and-answers session on chinuch at home.", 20, 18, "Kiryat Sefer Convention Center", 45m, new[] { "Gatherings", "Torah Classes" }),
            ("The Art of the Sofer STaM", "A sofer explains how a Sefer Torah, tefillin and mezuzos are written, with a live demonstration.", 22, 17, "Shaarei Chesed Hall", 30m, new[] { "Torah Classes" }),
            ("Niggunim from Home", "Old niggunim from our parents' homes, with piano and violin. Seating is fully separate.", 24, 18, "Beit Simcha Hall", 70m, new[] { "Niggun & Music" }),
            ("A Journey Through Eretz Yisrael", "A show for the whole family about the places in Eretz Yisrael mentioned in Tanach.", 26, 14, "Heichal HaTorah", 35m, new[] { "Kids & Family" }),
            ("Bochurim Gathering: Opening the Zman", "Words of chizuk before the new zman begins.", 28, 18, "Kiryat Sefer Convention Center", 25m, new[] { "Gatherings", "Chizuk & Emunah" }),
            ("Stories of Tzaddikim for Children", "A storyteller, niggunim and prizes.", 30, 15, "Shaarei Chesed Hall", 30m, new[] { "Kids & Family" }),
            ("The Wise Dayan", "A play about a dayan who settles quarrels with wisdom and fairness.", 32, 17, "Beit Simcha Hall", 55m, new[] { "Theater" }),
            ("Hilchos Brachos Series, Part 1", "The first meeting in a series on brachos on food.", 34, 18, "Shaarei Chesed Hall", 20m, new[] { "Torah Classes" }),
            ("Orchestra and Choir: Niggunei Yamim Noraim", "The tefillos of the Yamim Noraim arranged for orchestra and men's choir.", 36, 18, "Heichal HaTorah", 90m, new[] { "Niggun & Music" }),
            ("Hachnasas Sefer Torah Celebration", "The writing of the last letters, a procession and simcha. Seating is fully separate.", 38, 16, "Kiryat Sefer Convention Center", 30m, new[] { "Gatherings" }),
            ("Emunah and Bitachon Evening", "Chizuk on living with emunah and bitachon every day.", 40, 18, "Heichal HaTorah", 40m, new[] { "Chizuk & Emunah" }),
            ("Noach and the Teivah: A Puppet Show", "A puppet show with songs for young children.", 42, 14, "Shaarei Chesed Hall", 35m, new[] { "Theater", "Kids & Family" }),
            ("Night of Simcha Niggunim", "Simcha niggunim and dancing with a full orchestra. Seating is fully separate.", 44, 18, "Kiryat Sefer Convention Center", 110m, new[] { "Niggun & Music" }),
            ("Shiur: Opening of Bava Metzia", "An in-depth shiur on the first sugya, Shnayim Ochazin.", 46, 18, "Heichal HaTorah", 20m, new[] { "Torah Classes" }),
            ("Avos U'Banim Learning Night", "Fathers and sons learning together, with prizes and pizza.", 48, 16, "Ohel Yaakov Hall", 15m, new[] { "Torah Classes", "Kids & Family" }),
            ("Kumzitz by Candlelight", "Quiet niggunim and a few words of chizuk. Seating is fully separate.", 50, 18, "Ohel Yaakov Hall", 45m, new[] { "Niggun & Music", "Chizuk & Emunah" }),
            ("Hilchos Brachos Series, Part 2", "The second meeting: brachos rishonos and the order of priority.", 52, 18, "Shaarei Chesed Hall", 20m, new[] { "Torah Classes" }),
            ("The Story of Rabbi Akiva", "A play about how Rabbi Akiva started to learn at the age of forty.", 54, 17, "Beit Simcha Hall", 55m, new[] { "Theater" }),
            ("Kollel Yom Iyun: Hilchos Kashrus", "A full day of shiurim on kashrus in the modern kitchen.", 57, 8, "Kiryat Sefer Convention Center", 35m, new[] { "Torah Classes", "Gatherings" }),
            ("Children's Tehillim Gathering", "Hundreds of children saying Tehillim together.", 60, 14, "Heichal HaTorah", 10m, new[] { "Kids & Family", "Chizuk & Emunah" }),
            ("A Cappella Niggunim Evening", "A men's choir sings niggunim without instruments. Seating is fully separate.", 63, 18, "Ohel Yaakov Hall", 65m, new[] { "Niggun & Music" }),
            ("Lecture: Middos in Daily Life", "Practical ways to work on patience and kindness at home and at work.", 66, 18, "Shaarei Chesed Hall", 25m, new[] { "Torah Classes", "Chizuk & Emunah" }),
            ("Daf Yomi Siyum: Masechta Shabbos", "A siyum for the Daf Yomi learners of the city. Seating is fully separate.", 69, 17, "Kiryat Sefer Convention Center", 50m, new[] { "Gatherings" }),
            ("The Two Brothers' Fields", "A children's play based on the midrash about two loving brothers.", 72, 14, "Beit Simcha Hall", 40m, new[] { "Theater", "Kids & Family" }),
            ("Chazzanus Concert", "Classic tefillos sung by a chazzan and choir. Seating is fully separate.", 75, 18, "Heichal HaTorah", 100m, new[] { "Niggun & Music" }),
            ("Bein Hazmanim Family Fun Day", "Games, a show and a raffle for the whole family.", 79, 8, "Ohel Yaakov Hall", 30m, new[] { "Kids & Family" }),
            ("Hilchos Brachos Series, Part 3", "The last meeting: bracha achrona and common questions.", 83, 18, "Shaarei Chesed Hall", 20m, new[] { "Torah Classes" }),
            ("Teshuvah and Renewal Evening", "An evening of chizuk on starting again. Seating is fully separate.", 87, 17, "Heichal HaTorah", 30m, new[] { "Chizuk & Emunah" }),
            ("Mishnayos Siyum for Bochurim", "A siyum on Seder Moed by the bochurim of the yeshiva.", 91, 16, "Ohel Yaakov Hall", 25m, new[] { "Gatherings", "Torah Classes" }),
            ("Melava Malka with Niggunim", "A Motzaei Shabbos melava malka with a live band. Seating is fully separate.", 96, 19, "Beit Simcha Hall", 75m, new[] { "Niggun & Music" }),
            ("End-of-Year Family Celebration", "A show, niggunim and prizes for the children.", 104, 15, "Kiryat Sefer Convention Center", 45m, new[] { "Kids & Family", "Gatherings" }),
            ("Parsha Shiur for Bochurim", "A lively shiur on the parsha, with questions and answers.", 12, 18, "Ohel Yaakov Hall", 10m, new[] { "Torah Classes" }),
            ("Mussar Vaad: Working on Anavah", "A learning evening on humility, based on the words of the mussar sefarim.", 27, 18, "Ohel Yaakov Hall", 15m, new[] { "Torah Classes", "Chizuk & Emunah" }),
            ("Yeshiva Boys Choir Concert", "The choir of the yeshiva sings niggunim and songs of Torah. Seating is fully separate.", 33, 18, "Heichal HaTorah", 55m, new[] { "Niggun & Music" }),
            ("Hilchos Shabbos: The Shabbos Kitchen", "Practical halachos for cooking and warming food for Shabbos.", 38, 18, "Shaarei Chesed Hall", 15m, new[] { "Torah Classes" }),
            ("Hilchos Chanukah Shiur", "The halachos of lighting the menorah, explained with practical examples.", 41, 18, "Shaarei Chesed Hall", 15m, new[] { "Torah Classes", "Yamim Tovim" }),
            ("Chanukah Family Concert", "Niggunim, a children's choir and sufganiyos for the whole family.", 45, 16, "Beit Simcha Hall", 40m, new[] { "Yamim Tovim", "Kids & Family", "Niggun & Music" }),
            ("Kiddush Hashem Stories Evening", "True stories of people who sanctified the Name of Heaven, told by a maggid.", 47, 18, "Kiryat Sefer Convention Center", 30m, new[] { "Chizuk & Emunah", "Gatherings" }),
            ("Puppet Show: The Story of Yonah", "A puppet show with songs about Yonah and the people of Ninveh.", 55, 14, "Shaarei Chesed Hall", 30m, new[] { "Theater", "Kids & Family" }),
            ("Chesed Gemach Dinner", "A dinner for the benefit of the local gemach, with divrei Torah and niggunim. Seating is fully separate.", 61, 19, "Kiryat Sefer Convention Center", 150m, new[] { "Gatherings" }),
            ("Emunah Stories with a Maggid", "An evening of stories that strengthen emunah and bitachon.", 71, 18, "Heichal HaTorah", 25m, new[] { "Chizuk & Emunah" }),
            ("Tu BiShvat Seder for Families", "A seder with the fruits of Eretz Yisrael, divrei Torah and songs for the children.", 94, 16, "Ohel Yaakov Hall", 25m, new[] { "Yamim Tovim", "Kids & Family" })
        };

        foreach (var e in events)
        {
            var categoryIds = e.Categories.Select(name => categories[name]).ToArray();
            await CreateEventAsync(e.Title, e.Description, e.Days, e.HourUtc, halls[e.Hall], e.Price, categoryIds, ct);
        }
    }

    private async Task<int> CreateCategoryAsync(string name, CancellationToken ct)
    {
        return Unwrap(await _categoryService.CreateAsync(new CategoryRequest { Name = name }, ct)).Id;
    }

    private async Task<int> CreateHallAsync(string name, string address, List<int> rowSeatCounts, CancellationToken ct)
    {
        var request = new HallRequest { Name = name, Address = address, RowSeatCounts = rowSeatCounts };
        return Unwrap(await _hallService.CreateAsync(request, ct)).Id;
    }

    private async Task CreateEventAsync(
        string title, string description, int daysFromNow, int hourUtc, int hallId, decimal seatPrice, int[] categoryIds, CancellationToken ct)
    {
        var request = new EventRequest
        {
            Title = title,
            Description = description,
            StartsAt = DateTime.UtcNow.Date.AddDays(daysFromNow).AddHours(hourUtc),   // UTC, always in the future
            HallId = hallId,
            SeatPrice = seatPrice,
            CategoryIds = categoryIds.ToList()
        };

        Unwrap(await _eventService.CreateAsync(request, ct));
    }

    // A failure while seeding is a bug in the seed itself, so we stop loudly instead of continuing half-done.
    private static T Unwrap<T>(Result<T> result)
    {
        return result.IsSuccess
            ? result.Value!
            : throw new InvalidOperationException($"Seeding failed: {result.Error}");
    }
}
