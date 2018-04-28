using System;
using System.Diagnostics;

#pragma warning disable 1591
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable InconsistentNaming

namespace JetBrains.Annotations
{
	//README: these should all be marked as 'internal', so as not to conflict with the same attributes that would exist in the parent application !

	/// <summary>
	/// Indicates that the value of the marked element could be <c>null</c> sometimes,
	/// so the check for <c>null</c> is necessary before its usage.
	/// </summary>
	/// <example><code>
	/// [CanBeNull] object Test() => null;
	///
	/// void UseTest() {
	///   var p = Test();
	///   var s = p.ToString(); // Warning: Possible 'System.NullReferenceException'
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event |
	  AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.GenericParameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class CanBeNullAttribute : Attribute { }

	/// <summary>
	/// Indicates that the value of the marked element could never be <c>null</c>.
	/// </summary>
	/// <example><code>
	/// [NotNull] object Foo() {
	///   return null; // Warning: Possible 'null' assignment
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event |
	  AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.GenericParameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class NotNullAttribute : Attribute { }

	/// <summary>
	/// Can be appplied to symbols of types derived from IEnumerable as well as to symbols of Task
	/// and Lazy classes to indicate that the value of a collection item, of the Task.Result property
	/// or of the Lazy.Value property can never be null.
	/// </summary>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class ItemNotNullAttribute : Attribute { }

	/// <summary>
	/// Can be appplied to symbols of types derived from IEnumerable as well as to symbols of Task
	/// and Lazy classes to indicate that the value of a collection item, of the Task.Result property
	/// or of the Lazy.Value property can be null.
	/// </summary>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class ItemCanBeNullAttribute : Attribute { }

	/// <summary>
	/// Implicitly apply [NotNull]/[ItemNotNull] annotation to all the of type members and parameters
	/// in particular scope where this annotation is used (type declaration or whole assembly).
	/// </summary>
	[AttributeUsage(
	  AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly)]
	internal sealed class ImplicitNotNullAttribute : Attribute { }

	/// <summary>
	/// Indicates that the marked method builds string by format pattern and (optional) arguments.
	/// Parameter, which contains format string, should be given in constructor. The format string
	/// should be in <see cref="string.Format(IFormatProvider,string,object[])"/>-like form.
	/// </summary>
	/// <example><code>
	/// [StringFormatMethod("message")]
	/// void ShowError(string message, params object[] args) { /* do something */ }
	///
	/// void Foo() {
	///   ShowError("Failed: {0}"); // Warning: Non-existing argument in format string
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Constructor | AttributeTargets.Method |
	  AttributeTargets.Property | AttributeTargets.Delegate)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class StringFormatMethodAttribute : Attribute
	{
		/// <param name="formatParameterName">
		/// Specifies which parameter of an annotated method should be treated as format-string
		/// </param>
		public StringFormatMethodAttribute(string formatParameterName)
		{
			FormatParameterName = formatParameterName;
		}

		public string FormatParameterName { get; }
	}

	/// <summary>
	/// For a parameter that is expected to be one of the limited set of values.
	/// Specify fields of which type should be used as values for this parameter.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class ValueProviderAttribute : Attribute
	{
		public ValueProviderAttribute(string name)
		{
			Name = name;
		}

		[NotNull]
		public string Name { get; }
	}

	/// <summary>
	/// Indicates that the function argument should be string literal and match one
	/// of the parameters of the caller function. For example, ReSharper annotates
	/// the parameter of <see cref="System.ArgumentNullException"/>.
	/// </summary>
	/// <example><code>
	/// void Foo(string param) {
	///   if (param == null)
	///     throw new ArgumentNullException("par"); // Warning: Cannot resolve symbol
	/// }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Parameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class InvokerParameterNameAttribute : Attribute { }

	/// <summary>
	/// Describes dependency between method input and output.
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
	/// // A method that returns null if the parameter is null,
	/// // and not null if the parameter is not null
	/// [ContractAnnotation("null => null; notnull => notnull")]
	/// public object Transform(object data)
	/// </code></item>
	/// <item><code>
	/// [ContractAnnotation("s:null=>false; =>true,result:notnull; =>false, result:null")]
	/// public bool TryParse(string s, out Person result)
	/// </code></item>
	/// </list></examples>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class ContractAnnotationAttribute : Attribute
	{
		public ContractAnnotationAttribute([NotNull] string contract)
		  : this(contract, false) { }

		public ContractAnnotationAttribute([NotNull] string contract, bool forceFullStates)
		{
			Contract = contract;
			ForceFullStates = forceFullStates;
		}

		public string Contract { get; }
		public bool ForceFullStates { get; }
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
	/// 
	/// class UsesNoEquality {
	///   void Test() {
	///     var ca1 = new NoEquality();
	///     var ca2 = new NoEquality();
	///     if (ca1 != null) { // OK
	///       bool condition = ca1 == ca2; // Warning
	///     }
	///   }
	/// }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Struct)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class CannotApplyEqualityOperatorAttribute : Attribute { }

	/// <summary>
	/// When applied to a target attribute, specifies a requirement for any type marked
	/// with the target attribute to implement or inherit specific type or types.
	/// </summary>
	/// <example><code>
	/// [BaseTypeRequired(typeof(IComponent)] // Specify requirement
	/// class ComponentAttribute : Attribute { }
	/// 
	/// [Component] // ComponentAttribute requires implementing IComponent interface
	/// class MyComponent : IComponent { }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	[BaseTypeRequired(typeof(Attribute))]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class BaseTypeRequiredAttribute : Attribute
	{
		public BaseTypeRequiredAttribute([NotNull] Type baseType)
		{
			BaseType = baseType;
		}

		[NotNull]
		public Type BaseType { get; set; }
	}

	/// <summary>
	/// Indicates that the marked symbol is used implicitly (e.g. via reflection, in external library),
	/// so this symbol will not be marked as unused (as well as by other usage inspections).
	/// </summary>
	[AttributeUsage(AttributeTargets.All)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class UsedImplicitlyAttribute : Attribute
	{
		public UsedImplicitlyAttribute()
			: this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

		public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags)
			: this(useKindFlags, ImplicitUseTargetFlags.Default) { }

		public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
			: this(ImplicitUseKindFlags.Default, targetFlags) { }

		public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
		{
			UseKindFlags = useKindFlags;
			TargetFlags = targetFlags;
		}

		public ImplicitUseKindFlags UseKindFlags { get; }
		public ImplicitUseTargetFlags TargetFlags { get; }
	}

	/// <summary>
	/// Should be used on attributes and causes ReSharper to not mark symbols marked with such attributes
	/// as unused (as well as by other usage inspections)
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.GenericParameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class MeansImplicitUseAttribute : Attribute
	{
		public MeansImplicitUseAttribute()
			: this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

		public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags)
			: this(useKindFlags, ImplicitUseTargetFlags.Default) { }

		public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags)
			: this(ImplicitUseKindFlags.Default, targetFlags) { }

		public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
		{
			UseKindFlags = useKindFlags;
			TargetFlags = targetFlags;
		}

		[UsedImplicitly]
		public ImplicitUseKindFlags UseKindFlags { get; private set; }
		[UsedImplicitly]
		public ImplicitUseTargetFlags TargetFlags { get; private set; }
	}

	[Flags]
	internal enum ImplicitUseKindFlags
	{
		Default = Access | Assign | InstantiatedWithFixedConstructorSignature,
		/// <summary>Only entity marked with attribute considered used.</summary>
		Access = 1,
		/// <summary>Indicates implicit assignment to a member.</summary>
		Assign = 2,
		/// <summary>
		/// Indicates implicit instantiation of a type with fixed constructor signature.
		/// That means any unused constructor parameters won't be reported as such.
		/// </summary>
		InstantiatedWithFixedConstructorSignature = 4,
		/// <summary>Indicates implicit instantiation of a type.</summary>
		InstantiatedNoFixedConstructorSignature = 8,
	}

	/// <summary>
	/// Specify what is considered used implicitly when marked
	/// with <see cref="MeansImplicitUseAttribute"/> or <see cref="UsedImplicitlyAttribute"/>.
	/// </summary>
	[Flags]
	internal enum ImplicitUseTargetFlags
	{
		Default = Itself,
		Itself = 1,
		/// <summary>Members of entity marked with attribute are considered used.</summary>
		Members = 2,
		/// <summary>Entity marked with attribute and all its members considered used.</summary>
		WithMembers = Itself | Members
	}

	/// <summary>
	/// This attribute is intended to mark publicly available API
	/// which should not be removed and so is treated as used.
	/// </summary>
	[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class PublicAPIAttribute : Attribute
	{
		public PublicAPIAttribute() { }
		public PublicAPIAttribute([NotNull] string comment)
		{
			Comment = comment;
		}

		public string Comment { get; }
	}

	/// <summary>
	/// Tells code analysis engine if the parameter is completely handled when the invoked method is on stack.
	/// If the parameter is a delegate, indicates that delegate is executed while the method is executed.
	/// If the parameter is an enumerable, indicates that it is enumerated while the method is executed.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class InstantHandleAttribute : Attribute { }

	/// <summary>
	/// Indicates that a method does not make any observable state changes.
	/// The same as <c>System.Diagnostics.Contracts.PureAttribute</c>.
	/// </summary>
	/// <example><code>
	/// [Pure] int Multiply(int x, int y) => x * y;
	/// 
	/// void M() {
	///   Multiply(123, 42); // Waring: Return value of pure method is not used
	/// }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Method)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class PureAttribute : Attribute { }

	/// <summary>
	/// Indicates that the return value of method invocation must be used.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class MustUseReturnValueAttribute : Attribute
	{
		public MustUseReturnValueAttribute() { }
		public MustUseReturnValueAttribute([NotNull] string justification)
		{
			Justification = justification;
		}

		public string Justification { get; }
	}

	/// <summary>
	/// Indicates the type member or parameter of some type, that should be used instead of all other ways
	/// to get the value that type. This annotation is useful when you have some "context" value evaluated
	/// and stored somewhere, meaning that all other ways to get this value must be consolidated with existing one.
	/// </summary>
	/// <example><code>
	/// class Foo {
	///   [ProvidesContext] IBarService _barService = ...;
	/// 
	///   void ProcessNode(INode node) {
	///     DoSomething(node, node.GetGlobalServices().Bar);
	///     //              ^ Warning: use value of '_barService' field
	///   }
	/// }
	/// </code></example>
	[AttributeUsage(
	  AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Method |
	  AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.GenericParameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class ProvidesContextAttribute : Attribute { }

	/// <summary>
	/// Indicates how method, constructor invocation or property access
	/// over collection type affects content of the collection.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class CollectionAccessAttribute : Attribute
	{
		public CollectionAccessAttribute(CollectionAccessType collectionAccessType)
		{
			CollectionAccessType = collectionAccessType;
		}

		public CollectionAccessType CollectionAccessType { get; }
	}

	[Flags]
	internal enum CollectionAccessType
	{
		/// <summary>Method does not use or modify content of the collection.</summary>
		None = 0,
		/// <summary>Method only reads content of the collection but does not modify it.</summary>
		Read = 1,
		/// <summary>Method can change content of the collection but does not add new elements.</summary>
		ModifyExistingContent = 2,
		/// <summary>Method can add new elements to the collection.</summary>
		UpdatedContent = ModifyExistingContent | 4
	}

	/// <summary>
	/// Indicates that the marked method is assertion method, i.e. it halts control flow if
	/// one of the conditions is satisfied. To set the condition, mark one of the parameters with
	/// <see cref="AssertionConditionAttribute"/> attribute.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class AssertionMethodAttribute : Attribute { }

	/// <summary>
	/// Indicates the condition parameter of the assertion method. The method itself should be
	/// marked by <see cref="AssertionMethodAttribute"/> attribute. The mandatory argument of
	/// the attribute is the assertion type.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class AssertionConditionAttribute : Attribute
	{
		public AssertionConditionAttribute(AssertionConditionType conditionType)
		{
			ConditionType = conditionType;
		}

		public AssertionConditionType ConditionType { get; }
	}

	/// <summary>
	/// Specifies assertion type. If the assertion method argument satisfies the condition,
	/// then the execution continues. Otherwise, execution is assumed to be halted.
	/// </summary>
	internal enum AssertionConditionType
	{
		/// <summary>Marked parameter should be evaluated to true.</summary>
		IS_TRUE = 0,
		/// <summary>Marked parameter should be evaluated to false.</summary>
		IS_FALSE = 1,
		/// <summary>Marked parameter should be evaluated to null value.</summary>
		IS_NULL = 2,
		/// <summary>Marked parameter should be evaluated to not null value.</summary>
		IS_NOT_NULL = 3,
	}

	/// <summary>
	/// Indicates that method is pure LINQ method, with postponed enumeration (like Enumerable.Select,
	/// .Where). This annotation allows inference of [InstantHandle] annotation for parameters
	/// of delegate type by analyzing LINQ method chains.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class LinqTunnelAttribute : Attribute { }

	/// <summary>
	/// Indicates that IEnumerable, passed as parameter, is not enumerated.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class NoEnumerationAttribute : Attribute { }

	/// <summary>
	/// Indicates that parameter is regular expression pattern.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class RegexPatternAttribute : Attribute { }

	/// <summary>
	/// Prevents the Member Reordering feature from tossing members of the marked class.
	/// </summary>
	/// <remarks>
	/// The attribute must be mentioned in your member reordering patterns.
	/// </remarks>
	[AttributeUsage(AttributeTargets.All)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class NoReorder : Attribute { }

	// ====================================================
	// === CUSTOM CONTRACT ATTRIBUTES
	// ====================================================

	// NOTE: these attributes are not recognize by Resharper (yet?) but can be used
	// by Roslyn Analyzers or other static analysis tools to further verify the code.

	// DO NOT OVERWRITE THESE WHEN UPDATING THE OFFICAL CONTRACT ATTRIBUTES!

	/// <summary>The value cannot be negative</summary>
	/// <remarks>REQUIRES: x >= 0</remarks>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class PositiveAttribute : Attribute { }

	/// <summary>The value must be a power of two</summary>
	/// <remarks>REQUIRES: x == 1 &lt;&lt; Round(Log2(X))</remarks>
	[AttributeUsage(
	  AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
	  AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event)]
	[Conditional("JETBRAINS_ANNOTATIONS")]
	internal sealed class PowerOfTwoAttribute : Attribute { }

}
