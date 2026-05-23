using System.Configuration;
using System.Web.Mvc;
using VIRENDRA.Controllers;
using VIRENDRA.Data;

namespace VIRENDRA.App_Start
{
    public class DependencyInjectionConfig
    {
        public static void RegisterDependencies()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ConfigurationErrorsException("sqlconn is not configured in Web.config");
            }

            // Register repository
            var userRepository = new UserRepository(connectionString);

            // Setup controller factory
            ControllerBuilder.Current.SetControllerFactory(new CustomControllerFactory(userRepository));
        }
    }

    public class CustomControllerFactory : DefaultControllerFactory
    {
        private readonly IUserRepository _userRepository;

        public CustomControllerFactory(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        protected override IController GetControllerInstance(System.Web.Routing.RequestContext requestContext, System.Type controllerType)
        {
            if (controllerType == typeof(AuthController))
            {
                return new AuthController(_userRepository);
            }

            if (controllerType == typeof(ApiSettingsController))
            {
                return new ApiSettingsController(_userRepository);
            }

            if (controllerType == typeof(UserController))
            {
                return new UserController(_userRepository);
            }

            return base.GetControllerInstance(requestContext, controllerType);
        }
    }
}
