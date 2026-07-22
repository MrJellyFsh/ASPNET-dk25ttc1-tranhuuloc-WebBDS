using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaTot.Data;
using NhaTot.Models;
using NhaTot.Models.Enums;
using NhaTot.ViewModels;

namespace NhaTot.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var now = DateTime.UtcNow;
            var featured = await PropertiesController.ProjectCards(_context.Properties.AsNoTracking()
                    .Where(property => property.Status == PropertyStatus.Approved && (property.ExpiresAtUtc == null || property.ExpiresAtUtc > now))
                    .OrderByDescending(property => property.PublishedAtUtc).ThenByDescending(property => property.Id))
                .Take(6).ToListAsync();
            var provinces = await _context.Provinces.AsNoTracking().OrderBy(province => province.Name)
                .Select(province => new LookupOption(province.Id, province.Name)).ToListAsync();
            var propertyTypes = await _context.PropertyTypes.AsNoTracking().Where(type => type.IsActive).OrderBy(type => type.Name)
                .Select(type => new LookupOption(type.Id, type.Name)).ToListAsync();
            return View(new HomeViewModel { FeaturedProperties = featured, Provinces = provinces, PropertyTypes = propertyTypes });
        }
        catch (Exception exception) when (exception is Microsoft.Data.SqlClient.SqlException || exception.InnerException is Microsoft.Data.SqlClient.SqlException)
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi tải trang chủ.");
            return View(new HomeViewModel { IsDatabaseAvailable = false });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
