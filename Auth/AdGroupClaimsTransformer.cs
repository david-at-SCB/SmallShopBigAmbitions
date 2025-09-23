using System.Security.Claims;
using System.Security.Principal; // WindowsPrincipal
using Microsoft.AspNetCore.Authentication; // IClaimsTransformation
using Microsoft.AspNetCore.Authentication.Negotiate; // NegotiateDefaults
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics; // Debugger

namespace SmallShopBigAmbitions.Auth;

/// <summary>
/// Adds role claims based on on-prem AD group membership when using Negotiate auth.
/// Configuration examples (appsettings.json):
///   "AdSecurity": {
///      "GroupRoleMappings": "DOMAIN\\Finance=BillingAdmin;DOMAIN\\IT=Admin" ,
///      "AdminGroups": "DOMAIN\\IT",
///      "BreakOnTransform": true // OPTIONAL: if true triggers a Debugger.Break() at transform time (DEBUG only)
///   }
/// Each entry in GroupRoleMappings is GroupName=RoleName separated by ';'.
/// If user is member of the AD group (WindowsPrincipal.IsInRole) the Role claim is added.
/// Idempotent: adds marker claim ad_transform=1.
/// Additionally (DEBUG builds) we enumerate all WindowsIdentity groups, log them, and optionally break.
/// Adds a display_name claim (DOMAIN\\User) if missing so UI can render it consistently.
/// </summary>
public sealed class AdGroupClaimsTransformer : IClaimsTransformation
{
    private readonly IConfiguration _config;
    private readonly ILogger<AdGroupClaimsTransformer> _logger;

    public AdGroupClaimsTransformer(IConfiguration config, ILogger<AdGroupClaimsTransformer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<System.Security.Claims.ClaimsPrincipal> TransformAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        // Only act for Negotiate (Windows) identities
        var authType = principal.Identity?.AuthenticationType;
        //if (string.IsNullOrEmpty(authType) || !authType.Contains("Negotiate", StringComparison.OrdinalIgnoreCase))
        //    return Task.FromResult(principal);

        if (principal.HasClaim(c => c.Type == "ad_transform" && c.Value == "1"))
            return Task.FromResult(principal); // already processed

        if (principal.Identity is not System.Security.Claims.ClaimsIdentity identity)
            return Task.FromResult(principal);

        var windowsPrincipal = principal as WindowsPrincipal;
        if (windowsPrincipal == null && identity is WindowsIdentity winIdTemp)
            windowsPrincipal = new WindowsPrincipal(winIdTemp);

        if (windowsPrincipal == null)
            return Task.FromResult(principal); // cannot evaluate groups

        // Ensure display_name claim exists for UI (e.g., navbar). Use DOMAIN\User.
        var existingDisplay = identity.FindFirst("display_name")?.Value;
        var derivedName = identity.Name ?? windowsPrincipal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(existingDisplay) && !string.IsNullOrWhiteSpace(derivedName))
        {
            identity.AddClaim(new System.Security.Claims.Claim("display_name", derivedName));
        }

        // DEBUG diagnostics: enumerate all groups and optionally break
#if DEBUG
        try
        {
            if (windowsPrincipal.Identity is WindowsIdentity rawWin && rawWin.Groups is not null)
            {
                var groupDump = new List<string>();
                foreach (var sid in rawWin.Groups)
                {
                    try
                    {
                        var name = sid.Translate(typeof(NTAccount)) as NTAccount; // may throw
                        groupDump.Add(name?.Value ?? sid.Value);
                    }
                    catch
                    {
                        groupDump.Add(sid.Value);
                    }
                }
                _logger.LogInformation("[AD DEBUG] User {User} group memberships: {Groups}", identity.Name, string.Join("; ", groupDump));

                var breakFlag = _config.GetValue<bool?>("AdSecurity:BreakOnTransform") ?? false;
                if (breakFlag && !Debugger.IsAttached)
                {
                    // Trigger a break so developer can inspect 'groupDump' and 'identity'
                    Debugger.Launch(); // launches debugger if not attached
                }
                else if (breakFlag && Debugger.IsAttached)
                {
                    Debugger.Break(); // break into already attached debugger
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Group enumeration (debug) failed for {User}", identity.Name);
        }
#endif

        try
        {
            // Parse GroupRoleMappings
            var mapConfig = _config["AdSecurity:GroupRoleMappings"];
            if (!string.IsNullOrWhiteSpace(mapConfig))
            {
                foreach (var pair in mapConfig.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2) continue;
                    var group = parts[0];
                    var role = parts[1];
                    try
                    {
                        if (windowsPrincipal.IsInRole(group) && !identity.HasClaim(System.Security.Claims.ClaimTypes.Role, role))
                        {
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "IsInRole check failed for group {Group}", group);
                    }
                }
            }

            // Legacy AdminGroups support
            var adminGroupsConfig = _config["AdSecurity:AdminGroups"];
            if (!string.IsNullOrWhiteSpace(adminGroupsConfig) && !identity.HasClaim(System.Security.Claims.ClaimTypes.Role, "Admin"))
            {
                foreach (var g in adminGroupsConfig.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        if (windowsPrincipal.IsInRole(g))
                        {
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"));
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "IsInRole admin check failed for group {Group}", g);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AD group role mapping failed for {User}", identity.Name);
        }

        identity.AddClaim(new System.Security.Claims.Claim("ad_transform", "1"));
        return Task.FromResult(principal);
    }
}
