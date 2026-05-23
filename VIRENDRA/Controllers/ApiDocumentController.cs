using System;
using System.Collections.Generic;
using System.Web.Mvc;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.ApiUser)]
    public class ApiDocumentController : Controller
    {
        // GET: ApiDocument
        public ActionResult Index()
        {
            return View();
        }

        // GET: ApiDocument/RechargeAPI
        public ActionResult RechargeAPI()
        {
            return View();
        }

        // GET: ApiDocument/OperatorCode
        public ActionResult OperatorCode()
        {
            return View();
        }

        // GET: ApiDocument/CircleCode
        public ActionResult CircleCode()
        {
            return View();
        }

        // GET: ApiDocument/Balance
        public ActionResult Balance()
        {
            return View();
        }

        // GET: ApiDocument/Status
        public ActionResult Status()
        {
            return View();
        }

        // GET: ApiDocument/Complaint
        public ActionResult Complaint()
        {
            return View();
        }

        // GET: ApiDocument/FilesCallbackInfo
        public ActionResult FilesCallbackInfo()
        {
            return View();
        }
    }
}
