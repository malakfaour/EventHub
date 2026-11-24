using EventHub.Contracts;
using EventHub.Web.Models;
using EventHub.Web.Services.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventHub.Tests;

public class EventAdvisorTests
{
    [Fact]
    public async Task Without_an_api_key_the_rules_answer_with_valid_categories()
    {
        var advisor = new EventAdvisor(new HttpClient(), Options.Create(new AiOptions { ApiKey = "" }), NullLogger<EventAdvisor>.Instance);
        var ev = new Event { Id = Guid.NewGuid(), Name = "Gala", EventType = "Wedding", ExpectedGuests = 120 };

        var advice = await advisor.SuggestAsync(ev);

        Assert.Equal("Rules", advice.Source);
        Assert.NotEmpty(advice.Needs);
        Assert.All(advice.Needs, n => Assert.True(SupplyCategories.IsValid(n.Category)));
    }
}
