# Juice your .NET Expression Trees for everything they've got!

1. [Code reuse: invoke (i.e. embed) expressions within other, bigger expressions](#reuse).
2. [More code reuse: call [specially formatted] static and extension methods within expressions](#more-reuse).
3. 

## <a name="reuse"></a>Code Reuse: Invoke (i.e. embed) expressions within other, bigger expressions.

Ever wondered if you could save yourself from typing the exact same code snippets over and over
again while writing LINQ expressions for Entity Framework or LINQ-2-SQL? Well, now you can.

Just put your repeating code in a separate expression and then "invoke" it by using the **.Call()** extension method.
After you're done, call the Expand() method on the whole expression (or LINQ query) to have those **.Call()**
instances replaced with the original subexpression.

### Example:

**Before:**
```cs
if ( useShortName ) {
  query = from x in SomeTable
          where x.SomeFlag && (x.OtherFlag || x.ShortName == name) && !x.AnotherFlag
          select x;
} else {
  query = from x in SomeTable
          where x.SomeFlag && (x.OtherFlag || x.LongName == name) && !x.AnotherFlag
          select x;
}

return query;
```

**After:**
```cs
var name = useShortName
  ? Expr.Create( (SomeEntity x) => x.ShortName )
  : Expr.Create( (SomeEntity x) => x.LongName );

var query = from x in SomeTable
        where x.SomeFlag && (x.OtherFlag || name.Call( x ) == name) && !x.AnotherFlag
        select x;

return query.Expand();
```

## <a name="more-reuse"></a>More code reuse: call static/extension methods within expressions.

The same basic idea applies to using your own extension and/or static methods within expressions: just use them and call **.Expand()** afterwards.

There is one caveat: because static methods do not themselves get compiled to expression trees, you have to give us a hand here
by formatting your method in a special way: instead of putting the method body directly in the method body, it should be passed
as an argument to the Expr.Subst helper method.

###Example:

```cs
public static class Helpers {
	/// <summary>
	/// Define the static helper method by calling Expr.Subst(). 
	/// Pay attention to parameters: do not mix up the static method parameters with the expression tree parameters.
	/// </summary>
	public static string GetFullName( Person _p ) {
		return Expr.Subst( (Person p) => p.FirstName + " " + p.LastName );
	}
}

/// <summary>
/// Then use the helper in an expression tree. Don't forget to call Expand at the end.
/// </summary>
public IQueryable<Person> FindByFullName( string name ) {
	var result = from p in Database.Persons
							 where Helpers.GetFullName( p ) == name
							 select p;
							 
	return result.Expand();
}
```

###Example 2: make that an extension method
Because extension methods are basically just syntactic sugar for static methods, they will function in much the same way.

```cs
	public static string GetFullName( this Person _p ) {
		return Expr.Subst( (Person p) => p.FirstName + " " + p.LastName );
	}

	var result = from p in Database.Persons
	             where p.GetFullName() == name
	             select p;
```