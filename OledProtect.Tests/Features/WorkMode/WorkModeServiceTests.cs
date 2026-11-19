using OledProtect.Features.WorkMode.Services;

namespace OledProtect.Tests.Features.WorkMode
{
    public class WorkModeServiceTests
    {
        [Fact]
        public void Toggle_FlipsTheStateAndRaisesChangedEachTime()
        {
            var service = new WorkModeService();
            var raised = new List<bool>();
            service.Changed += raised.Add;

            service.Toggle();
            service.Toggle();

            Assert.False(service.IsEnabled);
            Assert.Equal(new[] { true, false }, raised);
        }

        [Fact]
        public void SetEnabled_ToTheCurrentState_DoesNotRaiseChanged()
        {
            var service = new WorkModeService();
            var count = 0;
            service.Changed += _ => count++;

            service.SetEnabled(false);

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task EnableFor_SwitchesOffByItselfAfterTheDuration()
        {
            var service = new WorkModeService();
            var raised = new List<bool>();
            service.Changed += raised.Add;

            service.EnableFor(TimeSpan.FromMilliseconds(150));
            Assert.True(service.IsEnabled);
            Assert.NotNull(service.EnabledUntil);

            for (int i = 0; i < 40 && service.IsEnabled; i++) await Task.Delay(50);

            Assert.False(service.IsEnabled);
            Assert.Null(service.EnabledUntil);
            Assert.Equal(new[] { true, false }, raised);
        }

        [Fact]
        public void SetEnabled_AfterEnableFor_CancelsTheTimer()
        {
            var service = new WorkModeService();
            service.EnableFor(TimeSpan.FromHours(1));

            service.SetEnabled(true);

            Assert.True(service.IsEnabled);
            Assert.Null(service.EnabledUntil);
        }
    }
}
