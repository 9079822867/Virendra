using System.Collections.Generic;

namespace VIRENDRA.Models
{
    /// <summary>Input data passed through the Recharge2 processing pipeline.</summary>
    public class RechargeModel
    {
        public int     UserId          { get; set; }
        public string  Username        { get; set; }
        public string  ApiToken        { get; set; }
        public string  MobileNo        { get; set; }
        public decimal Amount          { get; set; }
        public int?    OpId            { get; set; }
        public string  OpCode          { get; set; }
        public int?    CircleId        { get; set; }
        public string  CircleCode      { get; set; }
        public string  RefTxnId        { get; set; }   // user-supplied transaction id
        public string  OurTxnId        { get; set; }   // system-generated
        public string  IpAddress       { get; set; }
        public bool    IsROfferChecked { get; set; }
        public bool    IsROffer        { get; set; }
        public decimal ROfferAmount    { get; set; }
    }

    /// <summary>Mutable state carried through the routing and API-call pipeline.</summary>
    public class RechargeHelperDto
    {
        public int     SwitchId          { get; set; }
        public decimal DebitAmount       { get; set; }
        public decimal CommAmount        { get; set; }
        public List<ApiPriorityDto> ApiRouteList { get; set; } = new List<ApiPriorityDto>();
        public int     CurrentPriorityId { get; set; }
        public int     CurrentApiId      { get; set; }
        public long    RecId             { get; set; }
        public long    TxnId             { get; set; }
        public int     LapuId            { get; set; }
        public string  LapuNo            { get; set; }
        public string  LapuPass          { get; set; }
        public string  LapuPIN           { get; set; }
        public string  OP1               { get; set; }
        public string  OP2               { get; set; }
        public int     ApiTypeId         { get; set; }
        public int     RouteId           { get; set; }
        public int?    FTypeId           { get; set; }
        public string  RouteOP1          { get; set; }
        public int     RoutePriorityId   { get; set; }
        public decimal MinRO             { get; set; }
        public string  LapuFilter        { get; set; }
        public string  CircleFilter      { get; set; }
        public string  BlockUser         { get; set; }
        public string  UserFilter        { get; set; }
    }

    /// <summary>A single routing entry from CommanRouting / circle / amount tables.</summary>
    public class ApiPriorityDto
    {
        public int     ApiId            { get; set; }
        public int     PriorityId       { get; set; }
        public string  LapuFilter       { get; set; }
        public string  CircleFilter     { get; set; }
        public string  BlockUser        { get; set; }
        public string  UserFilter       { get; set; }
        public decimal MinRO            { get; set; }
        public int     RouteId          { get; set; }
        public int?    FTypeId          { get; set; }
        public string  RouteOP1         { get; set; }
        public int     RoutePriorityId  { get; set; }
        public string  AmountFilter     { get; set; }  // used for in-memory amount filtering
    }

    /// <summary>Result from SP_RechargeRequestValidation.</summary>
    public class RechargeValidationResult
    {
        public int     StatusCode    { get; set; }
        public string  StatusMsg     { get; set; }
        public int     SwitchTypeId  { get; set; }
        public int     Api1          { get; set; }
        public int     Api2          { get; set; }
        public int     Api3          { get; set; }
        public decimal DebitAmount   { get; set; }
        public decimal CommAmount    { get; set; }
        public bool    IsROffer      { get; set; }
        public decimal ROAmount      { get; set; }
    }

    /// <summary>Result from usp_RechargeCreate.</summary>
    public class CreateRechargeResult
    {
        public long    RecId      { get; set; }
        public long    TxnId      { get; set; }
        public decimal OpBal      { get; set; }
        public string  ApiUrl     { get; set; }
        public string  PostData   { get; set; }
        public int     LapuId     { get; set; }
        public string  LapuNo     { get; set; }
        public string  LapuPass   { get; set; }
        public string  LapuPIN    { get; set; }
        public string  OP1        { get; set; }
        public string  OP2        { get; set; }
        public int     ApiTypeId  { get; set; }
        public int     StatusCode { get; set; }
        public string  StatusMsg  { get; set; }
    }

    /// <summary>A tag-value rule used to classify an API response string.</summary>
    public class FilterTag
    {
        public int    Id       { get; set; }
        public int    ApiId    { get; set; }
        public string TagName  { get; set; }
        public string TagValue { get; set; }
        public int    StatusId { get; set; }  // 2=Success  3=Failed  5=Pending/Processing
    }

    public static class RouteType
    {
        public const int COMMON_ROUTE  = 1;
        public const int CIRCLE_WISE   = 2;
        public const int AMOUNT_WISE   = 3;
        public const int OPERATOR_WISE = 4;
    }

    public static class RechargeStatusCodes
    {
        public const int PENDING  = 1;
        public const int SUCCESS  = 2;
        public const int FAILED   = 3;
        public const int REFUND   = 4;
        public const int PROCESS  = 5;
    }
}
