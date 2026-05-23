using System.Configuration;
using System.Web.Mvc;
using VIRENDRA.Controllers;
using VIRENDRA.Data;
using VIRENDRA.Services;

namespace VIRENDRA.App_Start
{
    public class DependencyInjectionConfig
    {
        public static void RegisterDependencies()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
                throw new ConfigurationErrorsException("sqlconn is not configured in Web.config");

            var userRepository        = new UserRepository(connectionString);
            var packageRepository     = new PackageRepository();
            var walletRepository      = new WalletRepository();
            var vendorRepository      = new VendorRepository();
            var transactionRepository = new TransactionRepository();
            var rechargeRepository    = new RechargeRepository();
            var rechargeService       = new RechargeService(rechargeRepository);

            ControllerBuilder.Current.SetControllerFactory(
                new CustomControllerFactory(
                    userRepository, packageRepository, walletRepository,
                    vendorRepository, transactionRepository,
                    rechargeRepository, rechargeService));
        }
    }

    public class CustomControllerFactory : DefaultControllerFactory
    {
        private readonly IUserRepository        _userRepo;
        private readonly IPackageRepository     _pkgRepo;
        private readonly IWalletRepository      _walletRepo;
        private readonly IVendorRepository      _vendorRepo;
        private readonly ITransactionRepository _txnRepo;
        private readonly IRechargeRepository    _rechargeRepo;
        private readonly RechargeService        _rechargeSvc;

        public CustomControllerFactory(
            IUserRepository userRepo, IPackageRepository pkgRepo,
            IWalletRepository walletRepo, IVendorRepository vendorRepo,
            ITransactionRepository txnRepo,
            IRechargeRepository rechargeRepo, RechargeService rechargeSvc)
        {
            _userRepo      = userRepo;
            _pkgRepo       = pkgRepo;
            _walletRepo    = walletRepo;
            _vendorRepo    = vendorRepo;
            _txnRepo       = txnRepo;
            _rechargeRepo  = rechargeRepo;
            _rechargeSvc   = rechargeSvc;
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

            if (controllerType == typeof(BankAccountController))
                return new BankAccountController(_walletRepo);

            if (controllerType == typeof(VendorController))
                return new VendorController(_vendorRepo);

            if (controllerType == typeof(TransactionController))
                return new TransactionController(_txnRepo);

            if (controllerType == typeof(RechargeController))
                return new RechargeController(_rechargeSvc, _rechargeRepo);

            return base.GetControllerInstance(requestContext, controllerType);
        }
    }
}
