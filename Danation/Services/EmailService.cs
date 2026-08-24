using FluentEmail.Core;
using FluentEmail.Core.Models;

namespace Donation.Services;

public class EmailService
{
    private readonly IFluentEmail _fluentEmail;

    public EmailService(IFluentEmail fluentEmail)
    {
        _fluentEmail = fluentEmail;
    }

    public async Task<SendResponse> SendEmailAsync(string toEmail, string subject, string body)
    {
        SendResponse response = await _fluentEmail
            .To(toEmail)
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();
        return response;
    }
}

}
