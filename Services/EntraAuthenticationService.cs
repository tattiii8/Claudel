using System;
using System.Linq;
using System.Threading.Tasks;
using Claudel.Models;
using Microsoft.Identity.Client;

namespace Claudel.Services;

public sealed class EntraAuthenticationService
{
    private static readonly string[] Scopes =
    {
        "User.Read"
    };

    private readonly IPublicClientApplication _application;

    public IAccount? CurrentAccount { get; private set; }

    public AuthenticationResult? LastAuthenticationResult { get; private set; }

    public EntraAuthenticationService(
        EntraIdSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.TenantId))
        {
            throw new ArgumentException(
                "Entra ID Tenant ID is required.",
                nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new ArgumentException(
                "Entra ID Client ID is required.",
                nameof(settings));
        }

        _application =
            PublicClientApplicationBuilder
                .Create(settings.ClientId.Trim())
                .WithAuthority(
                    $"https://login.microsoftonline.com/{settings.TenantId.Trim()}")
                .WithDefaultRedirectUri()
                .Build();
    }

    public async Task<AuthenticationResult> SignInAsync()
    {
        var accounts =
            await _application.GetAccountsAsync();

        var account =
            accounts.FirstOrDefault();

        if (account != null)
        {
            try
            {
                var silentResult =
                    await _application
                        .AcquireTokenSilent(
                            Scopes,
                            account)
                        .ExecuteAsync();

                CurrentAccount =
                    silentResult.Account;

                LastAuthenticationResult =
                    silentResult;

                return silentResult;
            }
            catch (MsalUiRequiredException)
            {
                // Interactive login below.
            }
        }

        var interactiveResult =
            await _application
                .AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();

        CurrentAccount =
            interactiveResult.Account;

        LastAuthenticationResult =
            interactiveResult;

        return interactiveResult;
    }

    public async Task SignOutAsync()
    {
        var accounts =
            await _application.GetAccountsAsync();

        foreach (var account in accounts)
        {
            await _application.RemoveAsync(account);
        }

        CurrentAccount = null;
        LastAuthenticationResult = null;
    }

    public string? GetUserName()
    {
        return CurrentAccount?.Username;
    }
}