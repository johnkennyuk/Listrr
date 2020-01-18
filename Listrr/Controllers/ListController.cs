﻿using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Listrr.API.Trakt.Models.Filters;
using Listrr.Data;
using Listrr.Data.Trakt;
using Listrr.Jobs.BackgroundJobs;
using Listrr.Models;
using Listrr.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Listrr.Controllers
{
    public class ListController : Controller
    {
        private readonly AppDbContext _appDbContext;
        private readonly ITraktService _traktService;
        private readonly UserManager<User> _userManager;

        public ListController(AppDbContext appDbContext, ITraktService traktService, UserManager<User> userManager)
        {
            _appDbContext = appDbContext;
            _traktService = traktService;
            _userManager = userManager;
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> My()
        {
            ViewData["Message"] = "Overview of your lists";

            return View(await _traktService.Get(await _userManager.GetUserAsync(User)));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Scan(uint id)
        {
            var list = await _traktService.Get(id);
            if (list == null) return RedirectToAction(nameof(My));

            if (list.Owner.IsDonor && list.Owner.UserName == User.Identity.Name && list.ScanState == ScanState.None)
            {
                list.ScanState = ScanState.Scheduled;
                await _traktService.Update(list);

                Hangfire.BackgroundJob.Enqueue<ProcessDonorMovieListBackgroundJob>(x => x.Execute(list.Id));
            }

            return RedirectToAction(nameof(My));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MovieList()
        {
            ViewData["Message"] = "Create a new list for movies";

            var dbGenres = await _appDbContext.TraktMovieGenres.ToListAsync();
            var dbCertifications = await _appDbContext.TraktMovieCertifications.OrderBy(x => x.Description).ToListAsync();
            var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
            var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();

            var model = new CreateMovieListViewModel()
            {
                Genres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Slug)),
                Certifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description)),
                Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Name)),
                ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description)),
                ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description))
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MovieList(CreateMovieListViewModel model)
        {
            ViewData["Message"] = "Create a new list for movies";

            var dbGenres = await _appDbContext.TraktMovieGenres.ToListAsync();
            var dbCertifications = await _appDbContext.TraktMovieCertifications.ToListAsync();
            var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
            var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();

            model.Genres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Slug));
            model.Certifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description));
            model.Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name));
            model.Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Name));
            model.ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description));
            model.ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name));
            model.ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);

            var result = await _traktService.Create(new TraktList()
            {
                Name = model.Name,
                Query = model.Query ?? "",
                Type = ListType.Movie,
                Filter_Years = model.Filter_Years,
                Filter_Ratings = model.Filter_Ratings,
                Filter_Runtimes = model.Filter_Runtimes,
                SearchByAlias = model.SearchByAlias,
                SearchByBiography = model.SearchByBiography,
                SearchByDescription = model.SearchByDescription,
                SearchByName = model.SearchByName,
                SearchByOverview = model.SearchByOverview,
                SearchByPeople = model.SearchByPeople,
                SearchByTitle = model.SearchByTitle,
                SearchByTranslations = model.SearchByTranslations,
                SearchByTagline = model.SearchByTagline,
                Filter_Genres = new GenresCommonFilter(model.Filter_Genres),
                Filter_Languages = new LanguagesCommonFilter(model.Filter_Languages),
                Filter_Translations = new TranslationsBasicFilter(model.Filter_Translations),
                Filter_Certifications_Movie = new CertificationsMovieFilter(model.Filter_Certifications),
                Filter_Countries = new CountriesCommonFilter(model.Filter_Countries),
                Owner = user
            });
            
            if (user.IsDonor)
            {
                result.ReverseFilter_Genres = new GenresCommonFilter(model.ReverseFilter_Genres);
                result.ReverseFilter_Languages = new LanguagesCommonFilter(model.ReverseFilter_Languages);
                result.ReverseFilter_Countries = new CountriesCommonFilter(model.ReverseFilter_Countries);
                result.ReverseFilter_Translations = new TranslationsBasicFilter(model.ReverseFilter_Translations);
                result.ReverseFilter_Certifications_Movie = new CertificationsMovieFilter(model.ReverseFilter_Certifications);
            }

            if (user.IsDonor)
            {
                Hangfire.BackgroundJob.Enqueue<ProcessDonorMovieListBackgroundJob>(x => x.Execute(result.Id));
            }
            else
            {
                Hangfire.BackgroundJob.Enqueue<ProcessUserMovieListBackgroundJob>(x => x.Execute(result.Id));
            }

            return RedirectToAction(nameof(MovieList));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditMovieList(uint id)
        {
            var list = await _traktService.Get(id);

            if (list == null) return RedirectToAction(nameof(My));
            if (list.Type != ListType.Movie) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                var dbGenres = await _appDbContext.TraktMovieGenres.ToListAsync();
                var dbCertifications = await _appDbContext.TraktMovieCertifications.OrderBy(x => x.Description).ToListAsync();
                var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
                var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();

                return View(new EditMovieListViewModel
                {
                    Id = list.Id,
                    Genres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Name)),
                    Certifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description)),
                    Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                    Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktMovieGenre.Slug), nameof(TraktMovieGenre.Name)),
                    ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktMovieCertification.Slug), nameof(TraktMovieCertification.Description)),
                    ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                    ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    Name = list.Name,
                    Query = list.Query,
                    SearchByAlias = list.SearchByAlias,
                    SearchByBiography = list.SearchByBiography,
                    SearchByDescription = list.SearchByDescription,
                    SearchByName = list.SearchByName,
                    SearchByOverview = list.SearchByOverview,
                    SearchByPeople = list.SearchByPeople,
                    SearchByTitle = list.SearchByTitle,
                    SearchByTranslations = list.SearchByTranslations,
                    SearchByTagline = list.SearchByTagline,
                    Filter_Years = list.Filter_Years,
                    Filter_Runtimes = list.Filter_Runtimes,
                    Filter_Ratings = list.Filter_Ratings,
                    Filter_Genres = list.Filter_Genres.Genres,
                    Filter_Certifications = list.Filter_Certifications_Movie.Certifications,
                    Filter_Countries = list.Filter_Countries.Languages,
                    Filter_Languages = list.Filter_Languages.Languages,
                    Filter_Translations = list.Filter_Translations.Translations,
                    ReverseFilter_Genres = list.ReverseFilter_Genres?.Genres,
                    ReverseFilter_Certifications = list.ReverseFilter_Certifications_Movie?.Certifications,
                    ReverseFilter_Countries = list.ReverseFilter_Countries?.Languages,
                    ReverseFilter_Languages = list.ReverseFilter_Languages?.Languages,
                    ReverseFilter_Translations = list.ReverseFilter_Translations?.Translations
                });
            }

            return RedirectToAction(nameof(My));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditMovieList(EditMovieListViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var list = await _traktService.Get(model.Id);
            if (list == null) return RedirectToAction(nameof(My));
            if (list.Type != ListType.Movie) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                list.Name = model.Name;
                list.Query = model.Query ?? "";
                list.SearchByAlias = model.SearchByAlias;
                list.SearchByBiography = model.SearchByBiography;
                list.SearchByDescription = model.SearchByDescription;
                list.SearchByName = model.SearchByName;
                list.SearchByOverview = model.SearchByOverview;
                list.SearchByPeople = model.SearchByPeople;
                list.SearchByTitle = model.SearchByTitle;
                list.SearchByTranslations = model.SearchByTranslations;
                list.SearchByTagline = model.SearchByTagline;
                list.Filter_Years = model.Filter_Years;
                list.Filter_Runtimes = model.Filter_Runtimes;
                list.Filter_Ratings = model.Filter_Ratings;
                list.Filter_Genres = new GenresCommonFilter(model.Filter_Genres);
                list.Filter_Languages = new LanguagesCommonFilter(model.Filter_Languages);
                list.Filter_Translations = new TranslationsBasicFilter(model.Filter_Translations);
                list.Filter_Certifications_Movie = new CertificationsMovieFilter(model.Filter_Certifications);
                list.Filter_Countries = new CountriesCommonFilter(model.Filter_Countries);

                if (list.Owner.IsDonor)
                {
                    list.ReverseFilter_Genres = new GenresCommonFilter(model.ReverseFilter_Genres);
                    list.ReverseFilter_Languages = new LanguagesCommonFilter(model.ReverseFilter_Languages);
                    list.ReverseFilter_Translations = new TranslationsBasicFilter(model.ReverseFilter_Translations);
                    list.ReverseFilter_Certifications_Movie = new CertificationsMovieFilter(model.ReverseFilter_Certifications);
                    list.ReverseFilter_Countries = new CountriesCommonFilter(model.ReverseFilter_Countries);
                }

                await _traktService.Update(list, true);

                if (list.ScanState == ScanState.None)
                    if (list.Owner.IsDonor)
                    {
                        Hangfire.BackgroundJob.Enqueue<ProcessDonorMovieListBackgroundJob>(x => x.Execute(list.Id));
                    }
                    else
                    {
                        Hangfire.BackgroundJob.Enqueue<ProcessUserMovieListBackgroundJob>(x => x.Execute(list.Id));
                    }
                    

                return RedirectToAction(nameof(EditMovieList), new { list.Id });
            }

            return RedirectToAction(nameof(My));
        }
        

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ShowList()
        {
            ViewData["Message"] = "Create a new list for shows";

            var dbGenres = await _appDbContext.TraktShowGenres.ToListAsync();
            var dbCertifications = await _appDbContext.TraktShowCertifications.OrderBy(x => x.Description).ToListAsync();
            var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
            var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();
            var dbNetworks = await _appDbContext.TraktShowNetworks.OrderBy(x => x.Name).ToListAsync();
            var dbStatus = await _appDbContext.TraktShowStatuses.OrderBy(x => x.Name).ToListAsync();

            var model = new CreateShowListViewModel()
            {
                Genres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name)),
                Certifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description)),
                Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                Networks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name)),
                Status = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name)),
                ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name)),
                ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description)),
                ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                ReverseNetworks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name)),
                ReverseStatus = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name)),
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ShowList(CreateShowListViewModel model)
        {
            ViewData["Message"] = "Create a new list for shows";

            var dbGenres = await _appDbContext.TraktShowGenres.ToListAsync();
            var dbCertifications = await _appDbContext.TraktShowCertifications.ToListAsync();
            var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
            var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();
            var dbNetworks = await _appDbContext.TraktShowNetworks.ToListAsync();
            var dbStatus = await _appDbContext.TraktShowStatuses.ToListAsync();

            model.Genres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name));
            model.Certifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description));
            model.Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name));
            model.Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.Networks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name));
            model.Status = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name));
            model.ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name));
            model.ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description));
            model.ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name));
            model.ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description));
            model.ReverseNetworks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name));
            model.ReverseStatus = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name));

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);

            var result = await _traktService.Create(new TraktList()
            {
                Name = model.Name,
                Query = model.Query ?? "",
                Type = ListType.Show,
                Filter_Years = model.Filter_Years,
                Filter_Ratings = model.Filter_Ratings,
                Filter_Runtimes = model.Filter_Runtimes,
                SearchByAlias = model.SearchByAlias,
                SearchByBiography = model.SearchByBiography,
                SearchByDescription = model.SearchByDescription,
                SearchByName = model.SearchByName,
                SearchByOverview = model.SearchByOverview,
                SearchByPeople = model.SearchByPeople,
                SearchByTitle = model.SearchByTitle,
                SearchByTranslations = model.SearchByTranslations,
                Filter_Genres = new GenresCommonFilter(model.Filter_Genres),
                Filter_Languages = new LanguagesCommonFilter(model.Filter_Languages),
                Filter_Translations = new TranslationsBasicFilter(model.Filter_Translations),
                Filter_Certifications_Show = new CertificationsShowFilter(model.Filter_Certifications),
                Filter_Countries = new CountriesCommonFilter(model.Filter_Countries),
                Filter_Networks = new NetworksShowFilter(model.Filter_Networks),
                Filter_Status = new StatusShowFilter(model.Filter_Status),
                Owner = user
            });

            if (user.IsDonor)
            {
                result.ReverseFilter_Genres = new GenresCommonFilter(model.ReverseFilter_Genres);
                result.ReverseFilter_Languages = new LanguagesCommonFilter(model.ReverseFilter_Languages);
                result.ReverseFilter_Translations = new TranslationsBasicFilter(model.ReverseFilter_Translations);
                result.ReverseFilter_Certifications_Show = new CertificationsShowFilter(model.ReverseFilter_Certifications);
                result.ReverseFilter_Countries = new CountriesCommonFilter(model.ReverseFilter_Countries);
                result.ReverseFilter_Networks = new NetworksShowFilter(model.ReverseFilter_Networks);
                result.ReverseFilter_Status = new StatusShowFilter(model.ReverseFilter_Status);
            }

            if (user.IsDonor)
            {
                Hangfire.BackgroundJob.Enqueue<ProcessDonorShowListBackgroundJob>(x => x.Execute(result.Id));
            }
            else
            {
                Hangfire.BackgroundJob.Enqueue<ProcessUserShowListBackgroundJob>(x => x.Execute(result.Id));
            }

            return RedirectToAction(nameof(ShowList));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditShowList(uint id)
        {
            ViewData["Message"] = "Create a new list for shows";

            var list = await _traktService.Get(id);
            if (list == null) return RedirectToAction(nameof(My));
            if (list.Type != ListType.Show) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                var dbGenres = await _appDbContext.TraktShowGenres.ToListAsync();
                var dbCertifications = await _appDbContext.TraktShowCertifications.OrderBy(x => x.Description).ToListAsync();
                var dbCountryCodes = await _appDbContext.CountryCodes.OrderBy(x => x.Name).ToListAsync();
                var dbLanguageCodes = await _appDbContext.LanguageCodes.OrderBy(x => x.Code).ToListAsync();
                var dbNetworks = await _appDbContext.TraktShowNetworks.OrderBy(x => x.Name).ToListAsync();
                var dbStatus = await _appDbContext.TraktShowStatuses.OrderBy(x => x.Name).ToListAsync();

                return View(new EditShowListViewModel
                {
                    Id = list.Id,
                    Genres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name)),
                    Certifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description)),
                    Countries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                    Languages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    Translations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    Networks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name)),
                    Status = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name)),
                    ReverseGenres = new MultiSelectList(dbGenres, nameof(TraktShowGenre.Slug), nameof(TraktShowGenre.Name)),
                    ReverseCertifications = new MultiSelectList(dbCertifications, nameof(TraktShowCertification.Slug), nameof(TraktShowCertification.Description)),
                    ReverseCountries = new MultiSelectList(dbCountryCodes, nameof(CountryCode.Code), nameof(CountryCode.Name)),
                    ReverseLanguages = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    ReverseTranslations = new MultiSelectList(dbLanguageCodes, nameof(LanguageCode.Code), nameof(LanguageCode.Description)),
                    ReverseNetworks = new MultiSelectList(dbNetworks, nameof(TraktShowNetwork.Name), nameof(TraktShowNetwork.Name)),
                    ReverseStatus = new MultiSelectList(dbStatus, nameof(TraktShowStatus.Name), nameof(TraktShowStatus.Name)),
                    Name = list.Name,
                    Query = list.Query,
                    SearchByAlias = list.SearchByAlias,
                    SearchByBiography = list.SearchByBiography,
                    SearchByDescription = list.SearchByDescription,
                    SearchByName = list.SearchByName,
                    SearchByOverview = list.SearchByOverview,
                    SearchByPeople = list.SearchByPeople,
                    SearchByTitle = list.SearchByTitle,
                    SearchByTranslations = list.SearchByTranslations,
                    Filter_Years = list.Filter_Years,
                    Filter_Runtimes = list.Filter_Runtimes,
                    Filter_Ratings = list.Filter_Ratings,
                    Filter_Genres = list.Filter_Genres.Genres,
                    Filter_Certifications = list.Filter_Certifications_Show.Certifications,
                    Filter_Countries = list.Filter_Countries.Languages,
                    Filter_Languages = list.Filter_Languages.Languages,
                    Filter_Translations = list.Filter_Translations.Translations,
                    Filter_Networks = list.Filter_Networks.Networks,
                    Filter_Status = list.Filter_Status.Status,
                    ReverseFilter_Genres = list.ReverseFilter_Genres?.Genres,
                    ReverseFilter_Certifications = list.ReverseFilter_Certifications_Show?.Certifications,
                    ReverseFilter_Countries = list.ReverseFilter_Countries?.Languages,
                    ReverseFilter_Languages = list.ReverseFilter_Languages?.Languages,
                    ReverseFilter_Translations = list.ReverseFilter_Translations?.Translations,
                    ReverseFilter_Networks = list.ReverseFilter_Networks?.Networks,
                    ReverseFilter_Status = list.ReverseFilter_Status?.Status
                });
            }

            return RedirectToAction(nameof(My));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditShowList(EditShowListViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var list = await _traktService.Get(model.Id);
            if (list == null) return RedirectToAction(nameof(My));
            if (list.Type != ListType.Show) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                list.Name = model.Name;
                list.Query = model.Query ?? "";
                list.SearchByAlias = model.SearchByAlias;
                list.SearchByBiography = model.SearchByBiography;
                list.SearchByDescription = model.SearchByDescription;
                list.SearchByName = model.SearchByName;
                list.SearchByOverview = model.SearchByOverview;
                list.SearchByPeople = model.SearchByPeople;
                list.SearchByTitle = model.SearchByTitle;
                list.SearchByTranslations = model.SearchByTranslations;
                list.Filter_Years = model.Filter_Years;
                list.Filter_Runtimes = model.Filter_Runtimes;
                list.Filter_Ratings = model.Filter_Ratings;
                list.Filter_Genres = new GenresCommonFilter(model.Filter_Genres);
                list.Filter_Languages = new LanguagesCommonFilter(model.Filter_Languages);
                list.Filter_Translations = new TranslationsBasicFilter(model.Filter_Translations);
                list.Filter_Certifications_Show = new CertificationsShowFilter(model.Filter_Certifications);
                list.Filter_Countries = new CountriesCommonFilter(model.Filter_Countries);
                list.Filter_Networks = new NetworksShowFilter(model.Filter_Networks);
                list.Filter_Status = new StatusShowFilter(model.Filter_Status);
                
                if (list.Owner.IsDonor)
                {
                    list.ReverseFilter_Genres = new GenresCommonFilter(model.ReverseFilter_Genres);
                    list.ReverseFilter_Languages = new LanguagesCommonFilter(model.ReverseFilter_Languages);
                    list.ReverseFilter_Translations = new TranslationsBasicFilter(model.ReverseFilter_Translations);
                    list.ReverseFilter_Certifications_Show = new CertificationsShowFilter(model.ReverseFilter_Certifications);
                    list.ReverseFilter_Countries = new CountriesCommonFilter(model.ReverseFilter_Countries);
                    list.ReverseFilter_Networks = new NetworksShowFilter(model.ReverseFilter_Networks);
                    list.ReverseFilter_Status = new StatusShowFilter(model.ReverseFilter_Status);
                }

                await _traktService.Update(list, true);

                if (list.ScanState == ScanState.None)
                    if (list.Owner.IsDonor)
                    {
                        Hangfire.BackgroundJob.Enqueue<ProcessDonorShowListBackgroundJob>(x => x.Execute(list.Id));
                    }
                    else
                    {
                        Hangfire.BackgroundJob.Enqueue<ProcessUserShowListBackgroundJob>(x => x.Execute(list.Id));
                    }
                
                return RedirectToAction(nameof(EditShowList), new { list.Id });
            }

            return RedirectToAction(nameof(My));
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Delete(uint id)
        {
            var list = await _traktService.Get(id);
            if (list == null) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                return View(new DeleteListViewModel
                {
                    Id = list.Id,
                    Items = list.Items,
                    Name = list.Name
                });
            }

            return RedirectToAction(nameof(My));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Delete(DeleteListViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var list = await _traktService.Get(model.Id);

            if (list == null) return RedirectToAction(nameof(My));

            if (list.Owner.UserName == User.Identity.Name)
            {
                await _traktService.Delete(list, false);
            }

            return RedirectToAction(nameof(My));
        }
    }
}