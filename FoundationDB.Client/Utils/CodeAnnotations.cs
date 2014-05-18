using System;

namespace JetBrains.Annotations
{
	/// <summary>
	/// Indicates that the value of the marked element could be <c>null</c> sometimes,
	/// so the check for <c>null</c> is necessary before its usage
	/// </summary>
	/// <example><code>
	/// [CanBeNull] public object Test() { return null; }
	/// public void UseTest() {
	///   var p = Test();
	///   var s = p.ToString(); // Warning: Possible 'System.NullReferenceException'
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter |
	  AttributeTargets.Property | AttributeTargets.Delegate |
	  AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	internal sealed class CanBeNullAttribute : Attribute { }

	/// <summary>
	/// Indicates that the value of the marked element could never be <c>null</c>
	/// </summary>
	/// <example><code>
	/// [NotNull] public object Foo() {
	///   return null; // Warning: Possible 'null' assignment
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter |
	  AttributeTargets.Property | AttributeTargets.Delegate |
	  AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	internal sealed class NotNullAttribute : Attribute { }

	/// <summary>
	/// Indicates that the marked method builds string by format pattern and (optional) arguments.
	/// Parameter, which contains format string, should be given in constructor. The format string
	/// should be in <see cref="string.Format(IFormatProvider,string,object[])"/>-like form
	/// </summary>
	/// <example><code>
	/// [StringFormatMethod("message")]
	/// public void ShowError(string message, params object[] args) { /* do something */ }
	/// public void Foo() {
	///   ShowError("Failed: {0}"); // Warning: Non-existing argument in format string
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Constructor | AttributeTargets.Method,
	  AllowMultiple = false, Inherited = true)]
	internal sealed class StringFormatMethodAttribute : Attribute
	{
		/// <param name="formatParameterName">
		/// Specifies which parameter of an annotated method should be treated as format-string
		/// </param>
		public StringFormatMethodAttribute(string formatParameterName)
		{
			FormatParameterName = formatParameterName;
		}

		public string FormatParameterName { get; private set; }
	}

	/// <summary>
	/// Indicates that the function argument should be string literal and match one
	/// of the parameters of the caller function. For example, ReSharper annotates
	/// the parameter of <see cref="System.ArgumentNullException"/>
	/// </summary>
	/// <example><code>
	/// public void Foo(string param) {
	///   if (param == null)
	///     throw new ArgumentNullException("par"); // Warning: Cannot resolve symbol
	/// }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
	internal sealed class InvokerParameterNameAttribute : Attribute { }

	/// <summary>
	/// Describes dependency between method input and output
	/// </summary>
	/// <syntax>
	/// <p>Function Definition Table syntax:</p>
	/// <list>
	/// <item>FDT      ::= FDTRow [;FDTRow]*</item>
	/// <item>FDTRow   ::= Input =&gt; Output | Output &lt;= Input</item>
	/// <item>Input    ::= ParameterName: Value [, Input]*</item>
	/// <item>Output   ::= [ParameterName: Value]* {halt|stop|void|nothing|Value}</item>
	/// <item>Value    ::= true | false | null | notnull | canbenull</item>
	/// </list>
	/// If method has single input parameter, it's name could be omitted.<br/>
	/// Using <c>halt</c> (or <c>void</c>/<c>nothing</c>, which is the same)
	/// for method output means that the methos doesn't return normally.<br/>
	/// <c>canbenull</c> annotation is only applicable for output parameters.<br/>
	/// You can use multiple <c>[ContractAnnotation]</c> for each FDT row,
	/// or use single attribute with rows separated by semicolon.<br/>
	/// </syntax>
	/// <examples><list>
	/// <item><code>
	/// [ContractAnnotation("=> halt")]
	/// public void TerminationMethod()
	/// </code></item>
	/// <item><code>
	/// [ContractAnnotation("halt &lt;= condition: false")]
	/// public void Assert(bool condition, string text) // regular assertion method
	/// </code></item>
	/// <item><code>
	/// [ContractAnnotation("s:null => true")]
	/// public bool IsNullOrEmpty(string s) // string.IsNullOrEmpty()
	/// </code></item>
	/// <item><code>
	/// // A method that returns null if the parameter is null, and not null if the parameter is not null
	/// [ContractAnnotation("null => null; notnull => notnull")]
	/// public object Transform(object data) 
	/// </code></item>
	/// <item><code>
	/// [ContractAnnotation("s:null=>false; =>true,result:notnull; =>false, result:null")]
	/// public bool TryParse(string s, out Person result)
	/// </code></item>
	/// </list></examples>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	internal sealed class ContractAnnotationAttribute : Attribute
	{
		public ContractAnnotationAttribute([NotNull] string contract)
			: this(contract, false) { }

		public ContractAnnotationAttribute([NotNull] string contract, bool forceFullStates)
		{
			Contract = contract;
			ForceFullStates = forceFullStates;
		}

		public string Contract { get; private set; }
		public bool ForceFullStates { get; private set; }
	}

	/// <summary>
	/// Indicates that the value of the marked type (or its derivatives)
	/// cannot be compared using '==' or '!=' operators and <c>Equals()</c>
	/// should be used instead. However, using '==' or '!=' for comparison
	/// with <c>null</c> is always permitted.
	/// </summary>
	/// <example><code>
	/// [CannotApplyEqualityOperator]
	/// class NoEquality { }
	/// class UsesNoEquality {
	///   public void Test() {
	///     var ca1 = new NoEquality();
	///     var ca2 = new NoEquality();
	///     if (ca1 != null) { // OK
	///       bool condition = ca1 == ca2; // Warning
	///     }
	///   }
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Interface | AttributeTargets.Class |
	  AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
	internal sealed class CannotApplyEqualityOperatorAttribute : Attribute { }

	/// <summary>
	/// Tells code analysis engine if the parameter is completely handled
	/// when the invoked method is on stack. If the parameter is a delegate,
	/// indicates that delegate is executed while the method is executed.
	/// If the parameter is an enumerable, indicates that it is enumerated
	/// while the method is executed
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter, Inherited = true)]
	internal sealed class InstantHandleAttribute : Attribute { }

	/// <summary>
	/// Indicates that a method does not make any observable state changes.
	/// The same as <c>System.Diagnostics.Contracts.PureAttribute</c>
	/// </summary>
	/// <example><code>
	/// [Pure] private int Multiply(int x, int y) { return x * y; }
	/// public void Foo() {
	///   const int a = 2, b = 2;
	///   Multiply(a, b); // Waring: Return value of pure method is not used
	/// }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Method, Inherited = true)]
	internal sealed class PureAttribute : Attribute { }

}