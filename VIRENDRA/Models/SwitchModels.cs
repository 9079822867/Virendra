using System;
using System.Collections.Generic;

namespace VIRENDRA.Models
{
    /// <summary>SwitchType.Id — how a recharge picks its vendor list (must match RouteType).</summary>
    public static class SwitchTypes
    {
        public const int Common   = 1;   // CommanRouting
        public const int Operator = 2;   // Operator.API1_Id..API3_Id
        public const int Circle   = 3;   // CircleRouting.API1_Id..API3_Id
        public const int Amount   = 4;   // CommanRouting, amount filter only
    }

    /// <summary>One operator on Switch > Operator Switch (Operator table columns).</summary>
    public class OperatorSwitchRow
    {
        public int    OpId         { get; set; }
        public string OperatorName { get; set; }
        public string OperatorType { get; set; }
        public bool   IsActive     { get; set; }
        public int    SwitchTypeId { get; set; }
        public int    Api1         { get; set; }
        public int    Api2         { get; set; }
        public int    Api3         { get; set; }
        public int    CircleRoutes { get; set; }   // CircleRouting rows set for this operator
    }

    /// <summary>One circle on Switch > Circle Switch (CircleRouting row, or empty).</summary>
    public class CircleSwitchRow
    {
        public int  CircleId   { get; set; }
        public string CircleName { get; set; }
        public int  Api1       { get; set; }
        public int  Api2       { get; set; }
        public int  Api3       { get; set; }
        public bool IsROffer   { get; set; }
    }

    public class CircleSwitchViewModel
    {
        public int    OpId         { get; set; }
        public string OperatorName { get; set; }
        public int    SwitchTypeId { get; set; }
        public List<CircleSwitchRow> Rows { get; set; } = new List<CircleSwitchRow>();
    }

    /// <summary>BlockRoute row. SettingTypeId 1 = block ("Service Down"), 2 = accept but hold with SendStatusId.</summary>
    public class BlockRouteRow
    {
        public int      Id            { get; set; }
        public string   OpFilter      { get; set; }
        public string   CircleFilter  { get; set; }
        public string   UserFilter    { get; set; }
        public string   Amounts       { get; set; }
        public string   AmountRanges  { get; set; }
        public int?     SettingTypeId { get; set; }
        public int?     SendStatusId  { get; set; }
        public DateTime? AddedDate    { get; set; }
        public DateTime? UpdatedDate  { get; set; }
    }

    /// <summary>StopRouteMessage row: a failed vendor reply containing MsgFilter text stops switching.</summary>
    public class StopRouteMessageRow
    {
        public int       Id          { get; set; }
        public string    OpFilter    { get; set; }
        public string    ApiFilter   { get; set; }
        public string    MsgFilter   { get; set; }
        public DateTime? AddedDate   { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

    /// <summary>OperatorValidation row; one per operator per ColumnName (Number | Account | Amount).</summary>
    public class OperatorValidationRule
    {
        public int     Id              { get; set; }
        public int     OpId            { get; set; }
        public string  ColumnName      { get; set; }
        public int?    MinLenth        { get; set; }
        public int?    MaxLenth        { get; set; }
        public int?    MinVal          { get; set; }
        public int?    MaxVal          { get; set; }
        public string  StartWith       { get; set; }
        public bool    IsNum           { get; set; }
        public bool    IsMandatory     { get; set; }
        public string  ErrorMsg        { get; set; }
        public string  PlaceHolderText { get; set; }
    }

    public class OperatorValidationViewModel
    {
        public int    OpId         { get; set; }
        public string OperatorName { get; set; }
        public OperatorValidationRule Number  { get; set; }
        public OperatorValidationRule Account { get; set; }
        public OperatorValidationRule Amount  { get; set; }
    }

    public static class ValidationColumns
    {
        public const string Number  = "Number";
        public const string Account = "Account";
        public const string Amount  = "Amount";
        public static readonly string[] All = { Number, Account, Amount };
    }
}
