namespace IdentityService.Api.Onboarding;

public static class OnboardingConfiguration
{
    public static void Validate(IConfiguration configuration,IHostEnvironment environment)
    {
        if(!configuration.GetValue<bool>("ONBOARDING_ENABLED"))return;
        var root=configuration["FRONTEND_PUBLIC_URL"];
        if(!Uri.TryCreate(root,UriKind.Absolute,out var frontend)||frontend.AbsolutePath!="/"||frontend.Query!=""||frontend.Fragment!=""||frontend.UserInfo!=""||
            (frontend.Scheme!="https"&&!(environment.IsDevelopment()&&frontend.Scheme=="http"&&frontend.IsLoopback)))
            throw new InvalidOperationException("Configure FRONTEND_PUBLIC_URL as the trusted HTTPS frontend origin (loopback HTTP is allowed only in Development).");
        var documents=configuration["IDENTITY_DOCUMENT_ROOT"];
        if(string.IsNullOrWhiteSpace(documents)||!Path.IsPathFullyQualified(documents)||documents.Contains("wwwroot",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Configure IDENTITY_DOCUMENT_ROOT as an absolute private persistent directory outside public uploads.");
        Directory.CreateDirectory(documents);
        var client=configuration["GOOGLE_CLIENT_ID"];
        if(!string.IsNullOrWhiteSpace(client)&&!System.Text.RegularExpressions.Regex.IsMatch(client,@"^\d+-[A-Za-z0-9_-]+\.apps\.googleusercontent\.com$"))
            throw new InvalidOperationException("GOOGLE_CLIENT_ID must be a Google Web Client ID; never configure a client secret here.");
    }
}
