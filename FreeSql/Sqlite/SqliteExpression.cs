﻿using FreeSql.Internal;
using FreeSql.Internal.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace FreeSql.Sqlite {
	class SqliteExpression : CommonExpression {

		public SqliteExpression(CommonUtils common) : base(common) { }

		internal override string ExpressionLambdaToSqlOther(Expression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			switch (exp.NodeType) {
				case ExpressionType.Call:
					var callExp = exp as MethodCallExpression;
					var objExp = callExp.Object;
					var objType = objExp?.Type;
					if (objType?.FullName == "System.Byte[]") return null;

					var argIndex = 0;
					if (objType == null && callExp.Method.DeclaringType.FullName == typeof(Enumerable).FullName) {
						objExp = callExp.Arguments.FirstOrDefault();
						objType = objExp?.Type;
						argIndex++;
					}
					if (objType == null) objType = callExp.Method.DeclaringType;
					if (objType != null) {
						var left = objExp == null ? null : getExp(objExp);
						if (objType.IsArray == true) {
							switch (callExp.Method.Name) {
								case "Contains":
									//判断 in
									return $"({getExp(callExp.Arguments[argIndex])}) in {left}";
							}
						}
					}
					break;
				case ExpressionType.NewArrayInit:
					var arrExp = exp as NewArrayExpression;
					var arrSb = new StringBuilder();
					arrSb.Append("(");
					for (var a = 0; a < arrExp.Expressions.Count; a++) {
						if (a > 0) arrSb.Append(",");
						arrSb.Append(getExp(arrExp.Expressions[a]));
					}
					return arrSb.Append(")").ToString();
			}
			return null;
		}

		internal override string ExpressionLambdaToSqlMemberAccessString(MemberExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			if (exp.Expression == null) {
				switch (exp.Member.Name) {
					case "Empty": return "''";
				}
				return null;
			}
			var left = ExpressionLambdaToSql(exp.Expression, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			switch (exp.Member.Name) {
				case "Length": return $"length({left})";
			}
			return null;
		}
		internal override string ExpressionLambdaToSqlMemberAccessDateTime(MemberExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			if (exp.Expression == null) {
				switch (exp.Member.Name) {
					case "Now": return "datetime(current_timestamp,'localtime')";
					case "UtcNow": return "current_timestamp";
					case "Today": return "date(current_timestamp,'localtime')";
					case "MinValue": return "datetime('0001-01-01 00:00:00.000')";
					case "MaxValue": return "datetime('9999-12-31 23:59:59.999')";
				}
				return null;
			}
			var left = ExpressionLambdaToSql(exp.Expression, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			switch (exp.Member.Name) {
				case "Date": return $"date({left})";
				case "TimeOfDay": return $"strftime('%s',{left})";
				case "DayOfWeek": return $"strftime('%w',{left})";
				case "Day": return $"strftime('%d',{left})";
				case "DayOfYear": return $"strftime('%j',{left})";
				case "Month": return $"strftime('%m',{left})";
				case "Year": return $"strftime('%Y',{left})";
				case "Hour": return $"strftime('%H',{left})";
				case "Minute": return $"strftime('%M',{left})";
				case "Second": return $"strftime('%S',{left})";
				case "Millisecond": return $"(strftime('%f',{left})-strftime('%S',{left}))";
				case "Ticks": return $"(strftime('%s',{left})*10000000+621355968000000000)";
			}
			return null;
		}
		internal override string ExpressionLambdaToSqlMemberAccessTimeSpan(MemberExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			if (exp.Expression == null) {
				switch (exp.Member.Name) {
					case "Zero": return "0";
					case "MinValue": return "-922337203685.477580"; //秒 Ticks / 1000,000,0
					case "MaxValue": return "922337203685.477580";
				}
				return null;
			}
			var left = ExpressionLambdaToSql(exp.Expression, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			switch (exp.Member.Name) {
				case "Days": return $"floor(({left})/{60 * 60 * 24})";
				case "Hours": return $"floor(({left})/{60 * 60}%24)";
				case "Milliseconds": return $"(cast({left} as bigint)*1000)";
				case "Minutes": return $"floor(({left})/60%60)";
				case "Seconds": return $"(({left})%60)";
				case "Ticks": return $"(cast({left} as bigint)*10000000)";
				case "TotalDays": return $"(({left})/{60 * 60 * 24})";
				case "TotalHours": return $"(({left})/{60 * 60})";
				case "TotalMilliseconds": return $"(cast({left} as bigint)*1000)";
				case "TotalMinutes": return $"(({left})/60)";
				case "TotalSeconds": return $"({left})";
			}
			return null;
		}

		internal override string ExpressionLambdaToSqlCallString(MethodCallExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			if (exp.Object == null) {
				switch (exp.Method.Name) {
					case "IsNullOrEmpty":
						var arg1 = getExp(exp.Arguments[0]);
						return $"({arg1} is null or {arg1} = '')";
				}
			} else {
				var left = getExp(exp.Object);
				switch (exp.Method.Name) {
					case "StartsWith":
					case "EndsWith":
					case "Contains":
						var args0Value = getExp(exp.Arguments[0]);
						if (args0Value == "NULL") return $"({left}) IS NULL";
						if (exp.Method.Name == "StartsWith") return $"({left}) LIKE {(args0Value.EndsWith("'") ? args0Value.Insert(args0Value.Length - 1, "%") : $"({args0Value})||'%'")}";
						if (exp.Method.Name == "EndsWith") return $"({left}) LIKE {(args0Value.StartsWith("'") ? args0Value.Insert(1, "%") : $"'%'||({args0Value})")}";
						if (args0Value.StartsWith("'") && args0Value.EndsWith("'")) return $"({left}) LIKE {args0Value.Insert(1, "%").Insert(args0Value.Length, "%")}";
						return $"({left}) LIKE '%'||({args0Value})||'%'";
					case "ToLower": return $"lower({left})";
					case "ToUpper": return $"upper({left})";
					case "Substring":
						var substrArgs1 = getExp(exp.Arguments[0]);
						if (long.TryParse(substrArgs1, out var testtrylng1)) substrArgs1 = (testtrylng1 + 1).ToString();
						else substrArgs1 += "+1";
						if (exp.Arguments.Count == 1) return $"substr({left}, {substrArgs1})";
						return $"substr({left}, {substrArgs1}, {getExp(exp.Arguments[1])})";
					case "IndexOf":
						var indexOfFindStr = getExp(exp.Arguments[0]);
						//if (exp.Arguments.Count > 1 && exp.Arguments[1].Type.FullName == "System.Int32") {
						//	var locateArgs1 = getExp(exp.Arguments[1]);
						//	if (long.TryParse(locateArgs1, out var testtrylng2)) locateArgs1 = (testtrylng2 + 1).ToString();
						//	else locateArgs1 += "+1";
						//	return $"(instr({left}, {indexOfFindStr}, {locateArgs1})-1)";
						//}
						return $"(instr({left}, {indexOfFindStr})-1)";
					case "PadLeft":
						if (exp.Arguments.Count == 1) return $"lpad({left}, {getExp(exp.Arguments[0])})";
						return $"lpad({left}, {getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
					case "PadRight":
						if (exp.Arguments.Count == 1) return $"rpad({left}, {getExp(exp.Arguments[0])})";
						return $"rpad({left}, {getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
					case "Trim":
					case "TrimStart":
					case "TrimEnd":
						if (exp.Arguments.Count == 0) {
							if (exp.Method.Name == "Trim") return $"trim({left})";
							if (exp.Method.Name == "TrimStart") return $"ltrim({left})";
							if (exp.Method.Name == "TrimEnd") return $"rtrim({left})";
						}
						var trimArg1 = "";
						var trimArg2 = "";
						foreach (var argsTrim02 in exp.Arguments) {
							var argsTrim01s = new[] { argsTrim02 };
							if (argsTrim02.NodeType == ExpressionType.NewArrayInit) {
								var arritem = argsTrim02 as NewArrayExpression;
								argsTrim01s = arritem.Expressions.ToArray();
							}
							foreach (var argsTrim01 in argsTrim01s) {
								var trimChr = getExp(argsTrim01).Trim('\'');
								if (trimChr.Length == 1) trimArg1 += trimChr;
								else trimArg2 += $" || ({trimChr})";
							}
						}
						if (exp.Method.Name == "Trim") left = $"trim({left}, {_common.FormatSql("{0}", trimArg1)}{trimArg2})";
						if (exp.Method.Name == "TrimStart") left = $"ltrim({left}, {_common.FormatSql("{0}", trimArg1)}{trimArg2})";
						if (exp.Method.Name == "TrimEnd") left = $"rtrim({left}, {_common.FormatSql("{0}", trimArg1)}{trimArg2})";
						return left;
					case "Replace": return $"replace({left}, {getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
					case "CompareTo": return $"case when {left} = {getExp(exp.Arguments[0])} then 0 when {left} > {getExp(exp.Arguments[0])} then 1 else -1 end";
					case "Equals": return $"({left} = {getExp(exp.Arguments[0])})";
				}
			}
			throw new Exception($"SqliteExpression 未实现函数表达式 {exp} 解析");
		}
		internal override string ExpressionLambdaToSqlCallMath(MethodCallExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			switch (exp.Method.Name) {
				case "Abs": return $"abs({getExp(exp.Arguments[0])})";
				case "Sign": return $"sign({getExp(exp.Arguments[0])})";
				case "Floor": return $"floor({getExp(exp.Arguments[0])})";
				case "Ceiling": return $"ceiling({getExp(exp.Arguments[0])})";
				case "Round":
					if (exp.Arguments.Count > 1 && exp.Arguments[1].Type.FullName == "System.Int32") return $"round({getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
					return $"round({getExp(exp.Arguments[0])})";
				case "Exp": return $"exp({getExp(exp.Arguments[0])})";
				case "Log": return $"log({getExp(exp.Arguments[0])})";
				case "Log10": return $"log10({getExp(exp.Arguments[0])})";
				case "Pow": return $"power({getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
				case "Sqrt": return $"sqrt({getExp(exp.Arguments[0])})";
				case "Cos": return $"cos({getExp(exp.Arguments[0])})";
				case "Sin": return $"sin({getExp(exp.Arguments[0])})";
				case "Tan": return $"tan({getExp(exp.Arguments[0])})";
				case "Acos": return $"acos({getExp(exp.Arguments[0])})";
				case "Asin": return $"asin({getExp(exp.Arguments[0])})";
				case "Atan": return $"atan({getExp(exp.Arguments[0])})";
				case "Atan2": return $"atan2({getExp(exp.Arguments[0])}, {getExp(exp.Arguments[1])})";
				//case "Truncate": return $"truncate({getExp(exp.Arguments[0])}, 0)";
			}
			throw new Exception($"SqliteExpression 未实现函数表达式 {exp} 解析");
		}
		internal override string ExpressionLambdaToSqlCallDateTime(MethodCallExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			if (exp.Object == null) {
				switch (exp.Method.Name) {
					case "Compare": return $"(strftime('%s',{getExp(exp.Arguments[0])}) -strftime('%s',{getExp(exp.Arguments[1])}))";
					case "DaysInMonth": return $"strftime('%d',date({getExp(exp.Arguments[0])}||'-01-01',{getExp(exp.Arguments[1])}||' months','-1 days'))";
					case "Equals": return $"({getExp(exp.Arguments[0])} = {getExp(exp.Arguments[1])})";

					case "IsLeapYear":
						var isLeapYearArgs1 = getExp(exp.Arguments[0]);
						return $"(({isLeapYearArgs1})%4=0 AND ({isLeapYearArgs1})%100<>0 OR ({isLeapYearArgs1})%400=0)";

					case "Parse": return $"datetime({getExp(exp.Arguments[0])})";
					case "ParseExact":
					case "TryParse":
					case "TryParseExact": return $"datetime({getExp(exp.Arguments[0])})";
				}
			} else {
				var left = getExp(exp.Object);
				var args1 = exp.Arguments.Count == 0 ? null : getExp(exp.Arguments[0]);
				switch (exp.Method.Name) {
					case "Add": return $"datetime({left},({args1})||' seconds')";
					case "AddDays": return $"datetime({left},({args1})||' days')";
					case "AddHours": return $"datetime({left},({args1})||' hours')";
					case "AddMilliseconds": return $"datetime({left},(({args1})/1000)||' seconds')";
					case "AddMinutes": return $"datetime({left},({args1})||' seconds')";
					case "AddMonths": return $"datetime({left},({args1})||' months')";
					case "AddSeconds": return $"datetime({left},({args1})||' seconds')";
					case "AddTicks": return $"datetime({left},(({args1})/10000000)||' seconds')";
					case "AddYears": return $"datetime({left},({args1})||' years')";
					case "Subtract":
						switch ((exp.Arguments[0].Type.IsNullableType() ? exp.Arguments[0].Type.GenericTypeArguments.FirstOrDefault() : exp.Arguments[0].Type).FullName) {
							case "System.DateTime": return $"(strftime('%s',{left})-strftime('%s',{args1}))";
							case "System.TimeSpan": return $"datetime({left},(-{args1})||' seconds')";
						}
						break;
					case "Equals": return $"({left} = {getExp(exp.Arguments[0])})";
					case "CompareTo": return $"(strftime('%s',{left})-strftime('%s',{args1}))";
					case "ToString": return $"strftime('%Y-%m-%d %H:%M.%f',{left})";
				}
			}
			throw new Exception($"SqliteExpression 未实现函数表达式 {exp} 解析");
		}
		internal override string ExpressionLambdaToSqlCallTimeSpan(MethodCallExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			if (exp.Object == null) {
				switch (exp.Method.Name) {
					case "Compare": return $"({getExp(exp.Arguments[0])}-({getExp(exp.Arguments[1])}))";
					case "Equals": return $"({getExp(exp.Arguments[0])} = {getExp(exp.Arguments[1])})";
					case "FromDays": return $"(({getExp(exp.Arguments[0])})*{60 * 60 * 24})";
					case "FromHours": return $"(({getExp(exp.Arguments[0])})*{60 * 60})";
					case "FromMilliseconds": return $"(({getExp(exp.Arguments[0])})/1000)";
					case "FromMinutes": return $"(({getExp(exp.Arguments[0])})*60)";
					case "FromSeconds": return $"(({getExp(exp.Arguments[0])}))";
					case "FromTicks": return $"(({getExp(exp.Arguments[0])})/10000000)";
					case "Parse": return $"cast({getExp(exp.Arguments[0])} as bigint)";
					case "ParseExact":
					case "TryParse":
					case "TryParseExact": return $"cast({getExp(exp.Arguments[0])} as bigint)";
				}
			} else {
				var left = getExp(exp.Object);
				var args1 = exp.Arguments.Count == 0 ? null : getExp(exp.Arguments[0]);
				switch (exp.Method.Name) {
					case "Add": return $"({left}+{args1})";
					case "Subtract": return $"({left}-({args1}))";
					case "Equals": return $"({left} = {getExp(exp.Arguments[0])})";
					case "CompareTo": return $"({left}-({getExp(exp.Arguments[0])}))";
					case "ToString": return $"cast({left} as character)";
				}
			}
			throw new Exception($"SqliteExpression 未实现函数表达式 {exp} 解析");
		}
		internal override string ExpressionLambdaToSqlCallConvert(MethodCallExpression exp, List<SelectTableInfo> _tables, List<SelectColumnInfo> _selectColumnMap, Func<Expression[], string> getSelectGroupingMapString, SelectTableInfoType tbtype, bool isQuoteName) {
			Func<Expression, string> getExp = exparg => ExpressionLambdaToSql(exparg, _tables, _selectColumnMap, getSelectGroupingMapString, tbtype, isQuoteName);
			if (exp.Object == null) {
				switch (exp.Method.Name) {
					case "ToBoolean": return $"({getExp(exp.Arguments[0])} not in ('0','false'))";
					case "ToByte": return $"cast({getExp(exp.Arguments[0])} as int2)";
					case "ToChar": return $"substr(cast({getExp(exp.Arguments[0])} as character), 1, 1)";
					case "ToDateTime": return $"datetime({getExp(exp.Arguments[0])})";
					case "ToDecimal": return $"cast({getExp(exp.Arguments[0])} as decimal(36,18))";
					case "ToDouble": return $"cast({getExp(exp.Arguments[0])} as double)";
					case "ToInt16": 
					case "ToInt32": 
					case "ToInt64":
					case "ToSByte": return $"cast({getExp(exp.Arguments[0])} as smallint)";
					case "ToSingle": return $"cast({getExp(exp.Arguments[0])} as float)";
					case "ToString": return $"cast({getExp(exp.Arguments[0])} as character)";
					case "ToUInt16": return $"cast({getExp(exp.Arguments[0])} as unsigned)";
					case "ToUInt32": return $"cast({getExp(exp.Arguments[0])} as decimal(10,0))";
					case "ToUInt64": return $"cast({getExp(exp.Arguments[0])} as decimal(21,0))";
				}
			}
			throw new Exception($"SqliteExpression 未实现函数表达式 {exp} 解析");
		}
	}
}
