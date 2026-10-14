using System.ComponentModel.DataAnnotations;
﻿namespace IdentityService.Api.Dtos;

public class AdminResetPasswordRequest
{
    [StringLength(128)]
    public string? CustomTemporaryPassword { get; set; }
    public bool ForcePasswordChange { get; set; } = true;
}
