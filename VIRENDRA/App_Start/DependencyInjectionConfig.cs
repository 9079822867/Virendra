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
                throw new ConfigurationErrorsException("sqlconn is not configured in Web.config");

            var userRepository    = new UserRepository(connectionString);
            var packageRepository = new PackageRepository();

            ControllerBuilder.Current.SetControllerFactory(
                new CustomControllerFactory(userRepository, packageRepository));
        }
    }

    public class CustomControllerFactory : DefaultControllerFactory
    {
        private readonly IUserRepository    _userRepo;
        private readonly IPackageRepository _pkgRepo;

        public CustomControllerFactory(IUserRepository userRepo, IPackageRepository pkgRepo)
        {
            _userRepo = userRepo;
            _pkgRepo  = pkgRepo;
        }

        protected override IController GetControllerInstance(
            System.Web.Routing.RequestContext requestContext,
            System.Type controllerType)
        {
            if (controllerType == typeof(AuthController))
                return new AuthController(_userRepo);

            if (controllerType == typeof(ApiSettingsController))
                return new ApiSettingsController(_userRepo);

            if (controllerType == typeof(UserController))
                return new UserController(_userRepo, _pkgRepo);

            if (controllerType == typeof(PackageController))
                return new PackageController(_pkgRepo);

            if (controllerType == typeof(OperatorController))
                return new OperatorController(_pkgRepo);

            if (controllerType == typeof(PackageCommController))
                return new PackageCommController(_pkgRepo);

            return base.GetControllerInstance(requestContext, controllerType);
        }
    }
}
