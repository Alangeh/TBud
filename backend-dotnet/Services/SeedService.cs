using Microsoft.EntityFrameworkCore;

namespace TravelReview.Api.Services;

public class SeedService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SeedService> _log;
    public SeedService(AppDbContext db, ILogger<SeedService> log) { _db = db; _log = log; }

    public async Task RunAsync()
    {
        if (!await _db.Countries.AnyAsync())
        {
            _db.Countries.AddRange(Countries);
            await _db.SaveChangesAsync();
            _log.LogInformation("Seeded {N} countries", Countries.Count);
        }
        if (!await _db.Cities.AnyAsync())
        {
            _db.Cities.AddRange(Cities);
            await _db.SaveChangesAsync();
            _log.LogInformation("Seeded {N} cities", Cities.Count);
        }
        if (!await _db.Places.AnyAsync())
        {
            _db.Places.AddRange(Places);
            await _db.SaveChangesAsync();
            _log.LogInformation("Seeded {N} places", Places.Count);
        }
    }

    private static readonly List<CountryDoc> Countries = new()
    {
        new() { country_id = "c_italy", name = "Italy", code = "IT", description = "Renaissance art, coastal villages, and culinary mastery.", image = "https://images.unsplash.com/photo-1626628193008-fa2852153d79?w=800" },
        new() { country_id = "c_japan", name = "Japan", code = "JP", description = "Neon-lit cities and timeless tradition.", image = "https://images.pexels.com/photos/31376617/pexels-photo-31376617.png?w=800" },
        new() { country_id = "c_france", name = "France", code = "FR", description = "Wine country, alpine peaks, and Parisian charm.", image = "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800" },
        new() { country_id = "c_thailand", name = "Thailand", code = "TH", description = "Tropical beaches, temples, and street food paradise.", image = "https://images.unsplash.com/photo-1528181304800-259b08848526?w=800" },
        new() { country_id = "c_peru", name = "Peru", code = "PE", description = "Ancient ruins, Andean peaks, and Amazon rainforest.", image = "https://images.unsplash.com/photo-1526392060635-9d6019884377?w=800" },
    };

    private static readonly List<CityDoc> Cities = new()
    {
        new() { city_id = "ct_rome", country_id = "c_italy", name = "Rome", description = "The Eternal City.", image = "https://images.unsplash.com/photo-1552832230-c0197dd311b5?w=800" },
        new() { city_id = "ct_florence", country_id = "c_italy", name = "Florence", description = "Cradle of the Renaissance.", image = "https://images.unsplash.com/photo-1543429776-2782fc8e1acd?w=800" },
        new() { city_id = "ct_amalfi", country_id = "c_italy", name = "Amalfi Coast", description = "Cliffside villages over turquoise sea.", image = "https://images.unsplash.com/photo-1633321702518-7feccafb94d5?w=800" },
        new() { city_id = "ct_tokyo", country_id = "c_japan", name = "Tokyo", description = "Where future meets tradition.", image = "https://images.pexels.com/photos/31376617/pexels-photo-31376617.png?w=800" },
        new() { city_id = "ct_kyoto", country_id = "c_japan", name = "Kyoto", description = "Thousand-year-old temples.", image = "https://images.unsplash.com/photo-1493997181344-712f2f19d87a?w=800" },
        new() { city_id = "ct_osaka", country_id = "c_japan", name = "Osaka", description = "Japan's kitchen.", image = "https://images.unsplash.com/photo-1590559899731-a382839e5549?w=800" },
        new() { city_id = "ct_paris", country_id = "c_france", name = "Paris", description = "City of light.", image = "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800" },
        new() { city_id = "ct_nice", country_id = "c_france", name = "Nice", description = "Riviera glamour.", image = "https://images.unsplash.com/photo-1491166617655-0723a0999cfc?w=800" },
        new() { city_id = "ct_lyon", country_id = "c_france", name = "Lyon", description = "Gastronomic capital.", image = "https://images.unsplash.com/photo-1524396309943-e03f5249f002?w=800" },
        new() { city_id = "ct_bangkok", country_id = "c_thailand", name = "Bangkok", description = "Buzzing megacity.", image = "https://images.unsplash.com/photo-1563492065599-3520f775eeed?w=800" },
        new() { city_id = "ct_chiangmai", country_id = "c_thailand", name = "Chiang Mai", description = "Mountains and temples.", image = "https://images.unsplash.com/photo-1598935898639-81586f7d2129?w=800" },
        new() { city_id = "ct_phuket", country_id = "c_thailand", name = "Phuket", description = "Island paradise.", image = "https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?w=800" },
        new() { city_id = "ct_cusco", country_id = "c_peru", name = "Cusco", description = "Inca heartland.", image = "https://images.unsplash.com/photo-1531968455001-5c5272a41129?w=800" },
        new() { city_id = "ct_lima", country_id = "c_peru", name = "Lima", description = "Pacific culinary hub.", image = "https://images.unsplash.com/photo-1531219432768-9f540ce9714b?w=800" },
        new() { city_id = "ct_arequipa", country_id = "c_peru", name = "Arequipa", description = "White volcanic city.", image = "https://images.unsplash.com/photo-1580551023330-2b1c2a4bd91d?w=800" },
    };

    private static PlaceDoc P(string id, string city, string country, string name, string cat, string desc, string addr, string photo)
        => new() { place_id = id, city_id = city, country_id = country, name = name, category = cat, description = desc, address = addr, photos = new() { photo } };

    private static readonly List<PlaceDoc> Places = new()
    {
        P("p_colosseum", "ct_rome", "c_italy", "Colosseum", "attraction", "Iconic Roman amphitheater from 70 AD.", "Piazza del Colosseo, Rome", "https://images.unsplash.com/photo-1552832230-c0197dd311b5?w=800"),
        P("p_armando", "ct_rome", "c_italy", "Armando al Pantheon", "restaurant", "Family-run trattoria serving classic Roman dishes.", "Salita de' Crescenzi, Rome", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_uffizi", "ct_florence", "c_italy", "Uffizi Gallery", "attraction", "Renaissance masterpieces by Botticelli, da Vinci.", "Piazzale degli Uffizi, Florence", "https://images.unsplash.com/photo-1543429776-2782fc8e1acd?w=800"),
        P("p_positano", "ct_amalfi", "c_italy", "Positano Beach", "attraction", "Pebbled beach beneath pastel cliffside town.", "Positano, Amalfi", "https://images.unsplash.com/photo-1633321702518-7feccafb94d5?w=800"),
        P("p_lesirenuse", "ct_amalfi", "c_italy", "Le Sirenuse", "hotel", "Iconic pink-hued luxury hotel.", "Via Cristoforo Colombo, Positano", "https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"),
        P("p_senso_ji", "ct_tokyo", "c_japan", "Senso-ji Temple", "attraction", "Tokyo's oldest Buddhist temple.", "Asakusa, Tokyo", "https://images.unsplash.com/photo-1583400015750-72d6e1ef3c6c?w=800"),
        P("p_sukiyabashi", "ct_tokyo", "c_japan", "Sukiyabashi Jiro", "restaurant", "World-famous sushi master Jiro Ono.", "Ginza, Tokyo", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_park_hyatt_tokyo", "ct_tokyo", "c_japan", "Park Hyatt Tokyo", "hotel", "Sky-high luxury made famous by 'Lost in Translation'.", "Shinjuku, Tokyo", "https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"),
        P("p_fushimi", "ct_kyoto", "c_japan", "Fushimi Inari", "attraction", "Thousands of vermillion torii gates.", "Fushimi Ward, Kyoto", "https://images.unsplash.com/photo-1493997181344-712f2f19d87a?w=800"),
        P("p_kinkakuji", "ct_kyoto", "c_japan", "Kinkaku-ji", "attraction", "Golden Pavilion Zen temple.", "Kita Ward, Kyoto", "https://images.unsplash.com/photo-1545569310-99c9a9b53e80?w=800"),
        P("p_dotonbori", "ct_osaka", "c_japan", "Dotonbori", "attraction", "Neon-lit street food paradise.", "Chuo Ward, Osaka", "https://images.unsplash.com/photo-1590559899731-a382839e5549?w=800"),
        P("p_eiffel", "ct_paris", "c_france", "Eiffel Tower", "attraction", "Wrought-iron icon of Paris.", "Champ de Mars, Paris", "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"),
        P("p_septime", "ct_paris", "c_france", "Septime", "restaurant", "Neo-bistro tasting-menu hotspot.", "Rue de Charonne, Paris", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_ritz_paris", "ct_paris", "c_france", "Ritz Paris", "hotel", "Legendary palace hotel on Place Vendôme.", "Place Vendôme, Paris", "https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"),
        P("p_promenade", "ct_nice", "c_france", "Promenade des Anglais", "attraction", "Iconic seafront promenade.", "Nice", "https://images.unsplash.com/photo-1491166617655-0723a0999cfc?w=800"),
        P("p_paul_bocuse", "ct_lyon", "c_france", "Paul Bocuse", "restaurant", "Three-Michelin-star French heritage.", "Collonges-au-Mont-d'Or, Lyon", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_grand_palace", "ct_bangkok", "c_thailand", "Grand Palace", "attraction", "Royal complex glittering with gold.", "Phra Borom Maha Ratchawang, Bangkok", "https://images.unsplash.com/photo-1563492065599-3520f775eeed?w=800"),
        P("p_gaggan", "ct_bangkok", "c_thailand", "Gaggan Anand", "restaurant", "Progressive Indian dining theatre.", "Sukhumvit, Bangkok", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_mandarin_oriental_bk", "ct_bangkok", "c_thailand", "Mandarin Oriental Bangkok", "hotel", "Riverside grand dame since 1879.", "Charoenkrung Rd, Bangkok", "https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"),
        P("p_doi_suthep", "ct_chiangmai", "c_thailand", "Doi Suthep", "attraction", "Mountain temple with city views.", "Doi Suthep, Chiang Mai", "https://images.unsplash.com/photo-1598935898639-81586f7d2129?w=800"),
        P("p_phi_phi", "ct_phuket", "c_thailand", "Phi Phi Islands", "attraction", "Limestone cliffs and emerald lagoons.", "Phi Phi, Phuket", "https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?w=800"),
        P("p_machu_picchu", "ct_cusco", "c_peru", "Machu Picchu", "attraction", "Lost city of the Incas.", "Aguas Calientes, Cusco", "https://images.unsplash.com/photo-1526392060635-9d6019884377?w=800"),
        P("p_chinchero", "ct_cusco", "c_peru", "Chinchero Market", "attraction", "Andean weaving traditions.", "Chinchero, Cusco", "https://images.unsplash.com/photo-1531968455001-5c5272a41129?w=800"),
        P("p_central", "ct_lima", "c_peru", "Central", "restaurant", "World's Best Restaurant 2023 - elevation menus.", "Barranco, Lima", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_miraflores", "ct_lima", "c_peru", "Miraflores Cliffs", "attraction", "Pacific-facing parkland.", "Miraflores, Lima", "https://images.unsplash.com/photo-1531219432768-9f540ce9714b?w=800"),
        P("p_hotel_b", "ct_lima", "c_peru", "Hotel B", "hotel", "Belle Époque boutique mansion.", "Barranco, Lima", "https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"),
        P("p_santa_catalina", "ct_arequipa", "c_peru", "Santa Catalina Monastery", "attraction", "Citadel within the city, painted ochre and blue.", "Santa Catalina, Arequipa", "https://images.unsplash.com/photo-1580551023330-2b1c2a4bd91d?w=800"),
        P("p_zigzag", "ct_arequipa", "c_peru", "ZigZag Restaurant", "restaurant", "Andean meats on volcanic stone.", "Zela 210, Arequipa", "https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"),
        P("p_tsukiji", "ct_tokyo", "c_japan", "Tsukiji Outer Market", "attraction", "Street food and fresh seafood stalls.", "Tsukiji, Tokyo", "https://images.unsplash.com/photo-1583400015750-72d6e1ef3c6c?w=800"),
        P("p_seine_cruise", "ct_paris", "c_france", "Seine River Cruise", "attraction", "Float past Parisian monuments at sunset.", "Pont Neuf, Paris", "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"),
    };
}
