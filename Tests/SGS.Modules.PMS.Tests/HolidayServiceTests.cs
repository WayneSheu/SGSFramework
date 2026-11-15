using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Calendars;
using Xunit;

namespace SGS.Modules.PMS.Tests;

/// <summary>PMS 的工作日曆來源已改為 Core 的 IWorkCalendarProvider（CAL 模組實作）：驗證對應 WorkCalendar 與載入視窗。</summary>
public sealed class HolidayServiceTests
{
    private sealed class FakeProvider(params CalendarDayOverride[] overrides) : IWorkCalendarProvider
    {
        public DateOnly? From { get; private set; }

        public DateOnly? To { get; private set; }

        public Task<IReadOnlyList<CalendarDayOverride>> GetOverridesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            From = from;
            To = to;
            return Task.FromResult<IReadOnlyList<CalendarDayOverride>>(overrides);
        }
    }

    [Fact]
    public async Task LoadCalendar_MapsHolidayAndMakeUpDay()
    {
        var provider = new FakeProvider(
            new CalendarDayOverride(new DateOnly(2026, 11, 18), false, "公司休假"),   // 週三放假
            new CalendarDayOverride(new DateOnly(2026, 11, 28), true, "補班"));        // 週六上班
        var service = new HolidayService(provider);

        var calendar = await service.LoadCalendarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), CancellationToken.None);

        calendar.IsWorkday(new DateOnly(2026, 11, 18)).Should().BeFalse();
        calendar.IsHoliday(new DateOnly(2026, 11, 18)).Should().BeTrue();
        calendar.IsWorkday(new DateOnly(2026, 11, 28)).Should().BeTrue();
        calendar.IsWorkday(new DateOnly(2026, 11, 17)).Should().BeTrue();
        calendar.IsWorkday(new DateOnly(2026, 11, 29)).Should().BeFalse();
        provider.From.Should().Be(new DateOnly(2026, 11, 1));
        provider.To.Should().Be(new DateOnly(2026, 11, 30));
    }

    [Fact]
    public async Task LoadCalendar_Around_UsesSevenDaysBackAndFortyFiveForward_InTaipeiDate()
    {
        var provider = new FakeProvider();
        var service = new HolidayService(provider);

        // UTC 2026-11-15 17:00 ＝ 台灣 2026-11-16 01:00
        await service.LoadCalendarAsync(new DateTimeOffset(2026, 11, 15, 17, 0, 0, TimeSpan.Zero), CancellationToken.None);

        provider.From.Should().Be(new DateOnly(2026, 11, 9));
        provider.To.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task NullProvider_MeansPlainMondayToFriday()
    {
        var service = new HolidayService(new NullWorkCalendarProvider());

        var calendar = await service.LoadCalendarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), CancellationToken.None);

        calendar.IsWorkday(new DateOnly(2026, 11, 16)).Should().BeTrue();   // 週一
        calendar.IsWorkday(new DateOnly(2026, 11, 21)).Should().BeFalse();  // 週六
        calendar.AddWorkdays(new DateOnly(2026, 11, 20), 1).Should().Be(new DateOnly(2026, 11, 23)); // 週五 +1 工作日＝週一
    }

    [Fact]
    public async Task HolidayChangesSlaDueDate()
    {
        // 週一受理 +2 個工作日：週三放假 → 順延到週四
        var received = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(8));
        var withHoliday = await new HolidayService(new FakeProvider(new CalendarDayOverride(new DateOnly(2026, 11, 18), false, "放假")))
            .LoadCalendarAsync(received, CancellationToken.None);
        var plain = await new HolidayService(new NullWorkCalendarProvider()).LoadCalendarAsync(received, CancellationToken.None);

        SlaCalculator.Due(Infrastructure.Entities.TicketType.Incident, Infrastructure.Entities.TicketPriority.P3, received, withHoliday)
            .Should().Be(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(8)));
        SlaCalculator.Due(Infrastructure.Entities.TicketType.Incident, Infrastructure.Entities.TicketPriority.P3, received, plain)
            .Should().Be(new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8)));
    }
}
