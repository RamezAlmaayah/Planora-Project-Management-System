using System.Text.Encodings.Web;
using Planora.Application.Abstractions.Common;

namespace Planora.Web.Email;

/// <summary>
/// Builds Planora transactional email bodies with inline styles and table-based markup.
/// </summary>
public static class PlanoraEmailTemplates
{
    public const string ConfirmationSubject = "Confirm your Planora email";
    public const string PasswordResetSubject = "Reset your Planora password";

    public static EmailInlineResource ConfirmationIllustration =>
        LoadIllustration("confirm-email-lineart", "planora-confirm-email");

    public static EmailInlineResource PasswordResetIllustration =>
        LoadIllustration("reset-password-lineart", "planora-reset-password");

    public static string Confirmation(string? recipientName, string callbackUrl) =>
        Build(
            recipientName,
            "Confirm your Planora email",
            "Welcome to Planora",
            "Confirm your email",
            "Confirm your email address to finish setting up your account and get started with Planora.",
            "Confirm Email",
            callbackUrl,
            "If you did not create a Planora account, you can ignore this email.",
            "planora-confirm-email");

    public static string PasswordReset(string? recipientName, string callbackUrl) =>
        Build(
            recipientName,
            "Reset your Planora password",
            "Reset your password",
            "Password reset requested",
            "We received a request to reset the password for your Planora account. Use the button below to choose a new password.",
            "Reset Password",
            callbackUrl,
            "If you did not request a password reset, you can ignore this email. For your security, never share this link.",
            "planora-reset-password");

    private static string Build(
        string? recipientName,
        string title,
        string heading,
        string eyebrow,
        string message,
        string buttonText,
        string callbackUrl,
        string securityNote,
        string illustrationContentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Email action URL must be an absolute HTTP or HTTPS URL.", nameof(callbackUrl));
        }

        var safeName = string.IsNullOrWhiteSpace(recipientName)
            ? string.Empty
            : $"<p style=\"margin:0 0 20px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:25px;color:#263f52;\">Hi {HtmlEncoder.Default.Encode(recipientName)},</p>";
        var safeUrl = HtmlEncoder.Default.Encode(callbackUrl);
        var safeTitle = HtmlEncoder.Default.Encode(title);
        var safeHeading = HtmlEncoder.Default.Encode(heading);
        var safeEyebrow = HtmlEncoder.Default.Encode(eyebrow);
        var safeMessage = HtmlEncoder.Default.Encode(message);
        var safeButtonText = HtmlEncoder.Default.Encode(buttonText);
        var safeSecurityNote = HtmlEncoder.Default.Encode(securityNote);
        var safeIllustrationContentId = HtmlEncoder.Default.Encode(illustrationContentId);

        return $"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="x-apple-disable-message-reformatting">
  <title>{safeTitle}</title>
</head>
<body style="margin:0;padding:0;background-color:#eef3f7;">
  <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{safeHeading} - Planora Project Management Platform</div>
  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#eef3f7" style="width:100%;border-collapse:collapse;background-color:#eef3f7;">
    <tr>
      <td align="center" style="padding:32px 14px;">
        <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;border-collapse:separate;background-color:#ffffff;border:1px solid #dfe7ed;border-radius:14px;">
          <tr>
            <td height="5" bgcolor="#1b91bd" style="height:5px;background-color:#1b91bd;font-size:0;line-height:0;border-radius:14px 14px 0 0;">&nbsp;</td>
          </tr>
          <tr>
            <td align="center" style="padding:27px 24px 25px;border-bottom:1px solid #edf1f4;">
              <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:27px;line-height:34px;font-weight:700;letter-spacing:-0.5px;color:#17354a;">Planora</p>
              <p style="margin:3px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;letter-spacing:0.2px;color:#718596;">Project Management Platform</p>
            </td>
          </tr>
          <tr>
            <td align="center" style="padding:22px 24px 0;">
              <img src="cid:{safeIllustrationContentId}" alt="" width="260" style="display:block;width:260px;max-width:100%;height:auto;margin:0 auto;border:0;outline:none;text-decoration:none;" />
            </td>
          </tr>
          <tr>
            <td style="padding:38px 42px 34px;font-family:Arial,Helvetica,sans-serif;color:#17354a;">
              <p style="margin:0 0 11px;font-family:Arial,Helvetica,sans-serif;font-size:11px;line-height:16px;font-weight:700;letter-spacing:1.1px;text-transform:uppercase;color:#1683ad;">{safeEyebrow}</p>
              <h1 style="margin:0 0 19px;font-family:Arial,Helvetica,sans-serif;font-size:28px;line-height:36px;font-weight:700;letter-spacing:-0.45px;color:#17354a;">{safeHeading}</h1>
              {safeName}
              <p style="margin:0 0 28px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:27px;color:#405a6e;">{safeMessage}</p>
              <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:separate;">
                <tr>
                  <td align="center" bgcolor="#147fa8" style="border:1px solid #147fa8;border-radius:8px;background-color:#147fa8;">
                    <a href="{safeUrl}" style="display:inline-block;padding:16px 30px;border:1px solid #147fa8;border-radius:8px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:21px;font-weight:700;letter-spacing:0.1px;color:#ffffff;text-decoration:none;">{safeButtonText}</a>
                  </td>
                </tr>
              </table>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f5f8fa" style="width:100%;margin-top:28px;border-collapse:collapse;background-color:#f5f8fa;">
                <tr>
                  <td style="padding:15px 17px;border:1px solid #e8eef2;border-radius:7px;">
                    <p style="margin:0 0 6px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#687e8e;">If the button does not work, open this link:</p>
                    <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:19px;word-break:break-all;color:#167da3;"><a href="{safeUrl}" style="color:#167da3;text-decoration:underline;word-break:break-all;">{safeUrl}</a></p>
                  </td>
                </tr>
              </table>
              <p style="margin:23px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:21px;color:#718596;">{safeSecurityNote}</p>
            </td>
          </tr>
          <tr>
            <td align="center" style="padding:21px 24px 25px;border-top:1px solid #e9eef2;font-family:Arial,Helvetica,sans-serif;">
              <p style="margin:0;font-size:13px;line-height:20px;font-weight:700;color:#486174;">Planora</p>
              <p style="margin:3px 0 0;font-size:12px;line-height:18px;color:#8595a1;">Project Management Platform</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";
    }

    private static EmailInlineResource LoadIllustration(string fileName, string contentId)
    {
        var resourceName = $"Planora.Web.EmailAssets.{fileName}.png";
        using var stream = typeof(PlanoraEmailTemplates).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email illustration resource '{resourceName}' was not found.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return new EmailInlineResource(contentId, "image/png", content.ToArray());
    }
}
