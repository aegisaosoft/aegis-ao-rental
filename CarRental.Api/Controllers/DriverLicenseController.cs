/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CarRental.Api.Extensions;
using Microsoft.AspNetCore.StaticFiles;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriverLicenseController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DriverLicenseController> _logger;

    public DriverLicenseController(
        IWebHostEnvironment env,
        ILogger<DriverLicenseController> logger)
    {
        _env = env;
        _logger = logger;
    }

}
