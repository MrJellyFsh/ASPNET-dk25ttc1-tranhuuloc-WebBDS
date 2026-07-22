using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaTot.Data;
using NhaTot.Models;
using NhaTot.Models.Enums;
using NhaTot.ViewModels;
using System.Security.Claims;

namespace NhaTot.Controllers;

[Authorize]
public class AgentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(ApplicationDbContext context, ILogger<AgentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("Agents/Apply")]
    public async Task<IActionResult> Apply()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            if (await HasApprovedAgentAsync(userId))
            {
                TempData["InfoMessage"] = "Tài khoản của bạn đã có hồ sơ môi giới.";
                return RedirectToAction(nameof(Status));
            }

            if (await HasPendingApplicationAsync(userId))
            {
                TempData["InfoMessage"] = "Hồ sơ của bạn đang chờ xét duyệt.";
                return RedirectToAction(nameof(Status));
            }

            return View(new AgentApplicationInput());
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi mở hồ sơ môi giới.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("DatabaseUnavailable");
        }
    }

    [HttpPost("Agents/Apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(AgentApplicationInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            if (await HasApprovedAgentAsync(userId))
            {
                TempData["InfoMessage"] = "Tài khoản của bạn đã có hồ sơ môi giới.";
                return RedirectToAction(nameof(Status));
            }

            if (await HasPendingApplicationAsync(userId))
            {
                TempData["InfoMessage"] = "Hồ sơ của bạn đang chờ xét duyệt, nên chưa thể nộp thêm hồ sơ mới.";
                return RedirectToAction(nameof(Status));
            }

            if (!ModelState.IsValid) return View(input);

            _context.AgentApplications.Add(new AgentApplication
            {
                UserId = userId,
                FullName = input.FullName.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                CompanyName = TrimToNull(input.CompanyName),
                OperatingArea = input.OperatingArea.Trim(),
                YearsOfExperience = input.YearsOfExperience,
                Introduction = TrimToNull(input.Introduction),
                Status = AgentApplicationStatus.PendingReview
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Hồ sơ môi giới đã được gửi và đang chờ xét duyệt.";
            return RedirectToAction(nameof(Status));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Không thể lưu hồ sơ môi giới cho người dùng {UserId}.", userId);
            ModelState.AddModelError(string.Empty, "Không thể gửi hồ sơ lúc này. Có thể hồ sơ khác đang chờ xét duyệt. Vui lòng thử lại.");
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi gửi hồ sơ môi giới.");
            ModelState.AddModelError(string.Empty, "Dữ liệu đang tạm thời chưa sẵn sàng. Vui lòng thử lại sau.");
        }

        return View(input);
    }

    [HttpGet("Agents/Status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            var model = await _context.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new AgentApplicationStatusViewModel
                {
                    HasAgentProfile = user.AgentProfile != null,
                    DisplayName = user.AgentProfile != null ? user.AgentProfile.DisplayName : null,
                    CompanyName = user.AgentProfile != null ? user.AgentProfile.CompanyName : null,
                    OperatingArea = user.AgentProfile != null ? user.AgentProfile.OperatingArea : null,
                    Status = user.AgentProfile != null
                        ? AgentApplicationStatus.Approved
                        : user.AgentApplications.OrderByDescending(application => application.CreatedAtUtc)
                            .Select(application => (AgentApplicationStatus?)application.Status).FirstOrDefault(),
                    SubmittedAtUtc = user.AgentApplications.OrderByDescending(application => application.CreatedAtUtc)
                        .Select(application => (DateTime?)application.CreatedAtUtc).FirstOrDefault()
                })
                .SingleOrDefaultAsync();

            return View(model ?? new AgentApplicationStatusViewModel());
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi tải trạng thái hồ sơ môi giới.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("DatabaseUnavailable");
        }
    }

    private Task<bool> HasPendingApplicationAsync(string userId) => _context.AgentApplications.AsNoTracking()
        .AnyAsync(application => application.UserId == userId && application.Status == AgentApplicationStatus.PendingReview);

    private Task<bool> HasApprovedAgentAsync(string userId) => _context.AgentProfiles.AsNoTracking()
        .AnyAsync(profile => profile.UserId == userId);

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsDatabaseUnavailable(Exception exception) => exception is Microsoft.Data.SqlClient.SqlException
        || exception.InnerException is Microsoft.Data.SqlClient.SqlException;
}
