using System.Net;
using System.Text.RegularExpressions;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Auth;

public class AccountLoginTests : IClassFixture<AccountLoginTestHost>
{
    private readonly AccountLoginTestHost _host;

    public AccountLoginTests(AccountLoginTestHost host) => _host = host;

    [Theory]
    [InlineData("//evil.example/steal")]
    [InlineData("https://evil.example/steal")]
    [InlineData(@"\evil.example\steal")]
    [InlineData(@"/\evil.example/steal")]
    [InlineData(@"/connect\authorize?client_id=vokasia-bff")]
    [InlineData("/student/\u0085history")]
    public async Task GetLoginForm_UnsafeReturnUrl_UsesRootFallback(string unsafeReturnUrl)
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/account/login?returnUrl=" + Uri.EscapeDataString(unsafeReturnUrl));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "name=\"returnUrl\" value=\"/account/continue\"",
            html);
        Assert.DoesNotContain(
            $"name=\"returnUrl\" value=\"{WebUtility.HtmlEncode(unsafeReturnUrl)}\"",
            html);
    }

    [Theory]
    [InlineData("//evil.example/steal")]
    [InlineData("https://evil.example/steal")]
    [InlineData(@"\evil.example\steal")]
    [InlineData(@"/\evil.example/steal")]
    [InlineData(@"/connect\authorize?client_id=vokasia-bff")]
    [InlineData("/student/\u0085history")]
    public async Task PostLogin_ValidCredentialsWithUnsafeReturnUrl_RedirectsToRoot(
        string unsafeReturnUrl)
    {
        var user = await _host.SeedUserAsync(
            "unsafe-return-url",
            UserRole.TenantAdmin);
        var client = CreateClient();
        var form = new Dictionary<string, string>
        {
            ["email"] = user.Email!,
            ["password"] = AuthTestHelpers.Password,
            ["returnUrl"] = unsafeReturnUrl,
        };

        var response = await AuthTestHelpers.PostLoginFormAsync(client, form);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(
            "/account/continue",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostLogin_ValidCredentialsWithLocalReturnUrl_PreservesPathAndQuery()
    {
        const string returnUrl =
            "/connect/authorize?client_id=vokasia-bff&state=school%2Fjournal";
        var user = await _host.SeedUserAsync(
            "local-return-url",
            UserRole.TenantAdmin);
        var client = CreateClient();
        var form = new Dictionary<string, string>
        {
            ["email"] = user.Email!,
            ["password"] = AuthTestHelpers.Password,
            ["returnUrl"] = returnUrl,
        };

        var response = await AuthTestHelpers.PostLoginFormAsync(client, form);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostLogin_InactiveAndUnknownAccounts_ReturnSameGenericError()
    {
        var inactiveUser = await _host.SeedUserAsync(
            "inactive-account",
            UserRole.TenantAdmin,
            isActive: false);
        var client = CreateClient();
        var inactiveForm = new Dictionary<string, string>
        {
            ["email"] = inactiveUser.Email!,
            ["password"] = AuthTestHelpers.Password,
            ["returnUrl"] = "/account/continue",
        };
        var unknownForm = new Dictionary<string, string>
        {
            ["email"] = $"unknown-{Guid.NewGuid():N}@vokasia.test",
            ["password"] = AuthTestHelpers.Password,
            ["returnUrl"] = "/account/continue",
        };

        var inactiveResponse = await AuthTestHelpers.PostLoginFormAsync(
            client,
            inactiveForm);
        var unknownResponse = await AuthTestHelpers.PostLoginFormAsync(
            client,
            unknownForm);

        Assert.Equal(HttpStatusCode.SeeOther, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.SeeOther, unknownResponse.StatusCode);
        Assert.Equal(
            unknownResponse.Headers.Location?.OriginalString,
            inactiveResponse.Headers.Location?.OriginalString);
        Assert.Contains(
            Uri.EscapeDataString(
                "Email atau kata sandi salah. Periksa kembali lalu coba masuk."),
            inactiveResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostLogin_MissingAntiforgeryToken_IsRejectedBeforeSignIn()
    {
        var user = await _host.SeedUserAsync(
            "missing-antiforgery",
            UserRole.TenantAdmin);
        var client = CreateClient();
        var form = new Dictionary<string, string>
        {
            ["email"] = user.Email!,
            ["password"] = AuthTestHelpers.Password,
            ["returnUrl"] = "/connect/authorize",
        };

        var response = await client.PostAsync(
            "/account/login",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.StartsWith(
            "/account/login?returnUrl=%2Fconnect%2Fauthorize&error=",
            response.Headers.Location?.OriginalString);
        Assert.DoesNotContain(
            response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? cookies
                : [],
            cookie => cookie.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetLoginForm_RendersAccessibleSchoolThemedForm()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/account/login?error=" +
            Uri.EscapeDataString("Email atau kata sandi salah. Periksa kembali lalu coba masuk."));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\"",
            html);
        Assert.Contains("data-theme=\"sekolah\"", html);
        Assert.Contains("<label for=\"email\">Email</label>", html);
        Assert.Contains("id=\"email\"", html);
        Assert.Contains("autocomplete=\"username\"", html);
        Assert.Contains("<label for=\"password\">Kata sandi</label>", html);
        Assert.Contains("id=\"password\"", html);
        Assert.Contains("autocomplete=\"current-password\"", html);
        Assert.Contains("autocapitalize=\"none\"", html);
        Assert.Contains("spellcheck=\"false\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("id=\"login-error\"", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Equal(
            2,
            Regex.Matches(html, "aria-invalid=\"true\"").Count);
        Assert.Equal(
            2,
            Regex.Matches(html, "aria-describedby=\"login-error\"").Count);
        Assert.Contains(
            "Email atau kata sandi salah. Periksa kembali lalu coba masuk.",
            html);
        Assert.Contains(
            "Kata sandi dipakai hanya untuk memeriksa akunmu saat masuk.",
            html);
        Assert.Contains(
            "Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia.",
            html);
        Assert.Contains("id=\"password-toggle\"", html);
        Assert.Contains("type=\"button\"", html);
        Assert.Contains("aria-controls=\"password\"", html);
        Assert.Contains("aria-label=\"Tampilkan kata sandi\"", html);
        Assert.DoesNotContain("aria-pressed", html);
        Assert.Contains(">Tampilkan</button>", html);
        Assert.Contains("Sembunyikan kata sandi", html);
        Assert.Contains("id=\"password-status\"", html);
        Assert.Contains("aria-live=\"polite\"", html);
        Assert.Contains("Kata sandi ditampilkan.", html);
        Assert.Contains(".password-toggle", html);
        Assert.DoesNotContain(
            "background-color var(--dur-fast)",
            html);
        Assert.DoesNotContain(
            "border-color var(--dur-fast)",
            html);
        Assert.DoesNotContain("tidak disimpan di perangkat", html);
        Assert.Contains("--tap-min: 44px", html);
        Assert.Contains("min-block-size: var(--tap-min)", html);
        Assert.DoesNotContain("(dev)", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("form dev", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountContinue_RedirectsToConfiguredFrontendInsteadOfApiDeadEnd()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/account/continue");

        Assert.True(response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.SeeOther);
        Assert.Contains("http://localhost:3000/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task GetLoginForm_WithoutError_DoesNotMarkFieldsInvalid()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/account/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("aria-invalid=\"true\"", html);
        Assert.DoesNotContain("aria-describedby=\"login-error\"", html);
    }

    [Fact]
    public async Task GetLoginForm_UsesPerRequestStyleAndScriptNonceThatMatchesCsp()
    {
        var client = CreateClient();

        var firstResponse = await client.GetAsync("/account/login");
        var secondResponse = await client.GetAsync("/account/login");
        var firstHtml = await firstResponse.Content.ReadAsStringAsync();
        var secondHtml = await secondResponse.Content.ReadAsStringAsync();
        var firstNonce = ExtractStyleNonce(firstHtml);
        var secondNonce = ExtractStyleNonce(secondHtml);
        var firstScriptNonce = ExtractScriptNonce(firstHtml);
        var secondScriptNonce = ExtractScriptNonce(secondHtml);
        var firstCsp = GetSingleHeader(firstResponse, "Content-Security-Policy");
        var secondCsp = GetSingleHeader(secondResponse, "Content-Security-Policy");

        Assert.NotEqual(firstNonce, secondNonce);
        Assert.Equal(firstNonce, firstScriptNonce);
        Assert.Equal(secondNonce, secondScriptNonce);
        Assert.Contains("default-src 'none'", firstCsp);
        Assert.Contains($"style-src 'nonce-{firstNonce}'", firstCsp);
        Assert.Contains($"script-src 'nonce-{firstNonce}'", firstCsp);
        Assert.Contains("form-action 'self'", firstCsp);
        Assert.Contains("base-uri 'none'", firstCsp);
        Assert.Contains("frame-ancestors 'none'", firstCsp);
        Assert.DoesNotContain("'unsafe-inline'", firstCsp);
        Assert.Contains($"style-src 'nonce-{secondNonce}'", secondCsp);
        Assert.Contains($"script-src 'nonce-{secondNonce}'", secondCsp);
    }

    [Theory]
    [InlineData("/header-probe")]
    [InlineData("/account/login/extra")]
    public async Task NonLoginResponse_KeepsStrictDefaultCspWithoutStyleAllowance(
        string path)
    {
        var client = CreateClient();

        var response = await client.GetAsync(path);
        var csp = GetSingleHeader(response, "Content-Security-Policy");

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", csp);
        Assert.DoesNotContain("style-src", csp);
    }

    [Theory]
    [InlineData("/ACCOUNT/LOGIN")]
    [InlineData("/account/login/")]
    public async Task LoginRouteVariant_ReceivesNonceStyleCsp(string path)
    {
        var client = CreateClient();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var nonce = ExtractStyleNonce(html);
        var csp = GetSingleHeader(response, "Content-Security-Policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"style-src 'nonce-{nonce}'", csp);
        Assert.Contains($"script-src 'nonce-{nonce}'", csp);
        Assert.Contains("style-src 'nonce-", csp);
        Assert.Contains("form-action 'self'", csp);
    }

    private static string ExtractStyleNonce(string html)
    {
        var match = Regex.Match(
            html,
            """<style nonce="(?<nonce>[A-Za-z0-9+/_=-]+)">""");
        Assert.True(match.Success, "Login HTML must contain a nonce-protected style block.");
        return match.Groups["nonce"].Value;
    }

    private static string ExtractScriptNonce(string html)
    {
        var match = Regex.Match(
            html,
            """<script nonce="(?<nonce>[A-Za-z0-9+/_=-]+)">""");
        Assert.True(match.Success, "Login HTML must contain a nonce-protected script block.");
        return match.Groups["nonce"].Value;
    }

    private static string GetSingleHeader(
        HttpResponseMessage response,
        string headerName)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        return Assert.Single(values);
    }

    private HttpClient CreateClient() => _host.CreateClient();
}
