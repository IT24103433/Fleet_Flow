using System.Net;
using System.Text.RegularExpressions;
using FleetFlow.Contracts;

namespace NotificationService.Api.Email;

public static class EmailPresentation
{
    public static string Render(TransactionalEmail email)
    {
        static string Encode(string value)=>WebUtility.HtmlEncode(value);
        var verification=email.Template=="Verification";
        var code=verification?Regex.Match(email.Message,@"verification code is ([0-9]{6})",RegexOptions.IgnoreCase):Match.Empty;
        var category=verification?"Confirm it’s you":email.Template=="Welcome"?"Your journey starts here":email.Template=="Security"?"Account security":email.Template=="Support"?"Customer enquiry":"Your FleetFlow update";
        var message=Encode(email.Message).Replace("\n\n","</p><p style=\"margin:0 0 20px;line-height:1.75\">").Replace("\n","<br>");
        var codePanel=code.Success?$"<div style=\"margin:24px 0;padding:22px;background:#f1edf8;border:1px solid #e4daef;border-radius:14px;text-align:center;font:700 32px monospace;letter-spacing:10px;color:#5B1E91\">{code.Groups[1].Value}</div><p style=\"font-size:13px;color:#68616f\">Valid for ten minutes. Use it only on FleetFlow.</p>":"";
        if(code.Success)message=message.Replace(code.Groups[1].Value,"shown below");
        var action=Uri.TryCreate(email.ActionUrl,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http"&&string.IsNullOrEmpty(uri.UserInfo)
            ?$"<p style=\"margin:28px 0\"><a href=\"{Encode(email.ActionUrl!)}\" style=\"display:inline-block;padding:14px 24px;background:#5B1E91;color:#fff;text-decoration:none;border-radius:10px;font-weight:700\">{(email.Template=="Welcome"?"Explore your account":"View in FleetFlow")}</a></p>":"";
        return $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head><body style=\"margin:0;background:#f5f3ef;font:15px Arial,sans-serif;color:#25212b\"><div style=\"display:none;max-height:0;overflow:hidden\">{Encode(email.Subject)}</div><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"padding:32px 16px\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:600px;margin:auto;background:#fff;border:1px solid #e8e3ed;border-radius:20px;overflow:hidden\"><tr><td style=\"padding:28px 36px;background:#242029;color:#fff\"><strong style=\"font-size:24px;letter-spacing:-1px\">FleetFlow<span style=\"color:#bdb5e9\">.</span></strong><span style=\"display:block;margin-top:6px;font-size:11px;letter-spacing:2px;color:#c8bdd6\">AUTOMATIONS</span></td></tr><tr><td style=\"padding:36px\"><p style=\"color:#6d4c91;font-size:12px;font-weight:700;margin:0 0 12px\">{category}</p><h1 style=\"margin:0 0 24px;font-size:28px;line-height:1.25;letter-spacing:-.8px\">{Encode(email.Subject)}</h1><p style=\"margin:0 0 20px;line-height:1.75\">{message}</p>{codePanel}{action}<hr style=\"border:0;border-top:1px solid #eee8f3;margin:28px 0\"><p style=\"font-size:12px;line-height:1.7;color:#746d7c;margin:0\">Sent by FleetFlow Automations. Never share verification codes or passwords. If you didn’t request an account action, you can ignore its code.</p></td></tr></table><p style=\"text-align:center;color:#857c8c;font-size:12px\">FleetFlow · Your plans. Your next drive.</p></td></tr></table></body></html>";
    }
}
