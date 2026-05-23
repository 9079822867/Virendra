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
            var walletRepository  = new WalletRepository();

            ControllerBuilder.Current.SetControllerFactory(
                new CustomControllerFactory(userRepository, packageRepository, walletRepository));
        }
    }

    public class CustomControllerFactory : DefaultControllerFactory
    {
        private readonly IUserRepository    _userRepo;
        private readonly IPackageRepository _pkgRepo;
        private readonly IWalletRepository  _walletRepo;

        public CustomControllerFactory(IUserRepository userRepo, IPackageRepository pkgRepo, IWalletRepository walletRepo)
        {
            _userRepo   = userRepo;
            _pkgRepo    = pkgRepo;
            _walletRepo = walletRepo;
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

            if (controllerType == typeof(WalletController))
                return new WalletController(_walletRepo, _userRepo);

            return base.GetControllerInstance(requestContext, controllerType);
        }
    }
}
