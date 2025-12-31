// Persistent global locator
namespace ServiceLocatorPattern
{
    public class GlobalServiceLocator : ServiceLocator<GlobalServiceLocator>
    {
        protected override void Awake()
        {
            this.isPersistent = true;
            base.Awake();
        }
    }

}