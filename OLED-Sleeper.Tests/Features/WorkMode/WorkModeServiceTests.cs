using OLED_Sleeper.Features.WorkMode.Services;

namespace OLED_Sleeper.Tests.Features.WorkMode
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
    }
}
