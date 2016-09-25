# Juice your .NET Expression Trees for everything they've got!
[![Build Status](https://travis-ci.org/erecruit/erecruit.Expr.svg?branch=master)](https://travis-ci.org/erecruit/erecruit.Expr)

1. [Code reuse: invoke (i.e. embed) expressions within other, bigger expressions](#reuse).
2. [More code reuse: call [specially formatted] static and extension methods within expressions](#more-reuse).
3. [Expression Creation, Simplified](#creation)
4. [Expression Composition](#composition)
5. [Predicate Composition](#predicate-composition)
6. [Handling Lists of Expressions](#lists)
7. [Obtain a property setter from a property expression](#setter)

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

## <a name="creation"></a>Expression Creation, Simplified.

When you want to pass an expression to a method, such as `IEnumerable.Select` or `IEnumerable.Where`, the compiler
can infer the necessary types for you, but what if you just want to put it in a variable for later?

```cs
	Expression<Func<Customer, IEnumerable<Order>>> orders = c => c.Orders;
```

And what if the return type happens to be anonymous?

```cs
	Expression<Func<Customer, ????>> orders = c => new { id = c.ID, orders = c.Orders };
```

A bit clunky, isn't it? But fear not! With `erecruit.Expr.Create` method, you can take advantage of the compiler type inference.
Unfortunately, you still have to specify the input type (everything has limits, you know):

```cs
	var orders = Expr.Create( (Customer c) => new { id = c.ID, orders = c.Orders } );
```

## <a name="composition"></a>Expression Composition.

We mean "Composition" in the computer science sense. You know, the way you would compose functions. Only with expressions:

```cs
	var customerFromOrder = Expr.Create( (Order o) => o.Customer );
	var customerIDFromOrder = customerFromOrder.Compose( c => c.ID ); // Equivalent to "o => o.Customer.ID"

	IQueryable<Order> orders = ...;
	var custIDs = orders.Select( customerIDFromOrder );
```

## <a name="predicate-composition"></a>Predicate Composition.

Predicates are expressions that return `bool`. Ergo, they ought to be subject to Boolean algebra:

```cs
	var isProcessed = Expr.Create( (Order o) => o.IsProcessed );
	var isProcessedAndOld = isProcessed.And( o => o.Date < DateTime.Today ); // try also: ".Or()"
	
	IQueryable<Order> orders = ...;
	var oldProcessedOrders = orders.Where( isProcessedAndOld );
```

## <a name="lists"></a>Handling Expression Lists.

If you have a list of expressions, you can `fold` over it, achiving some interesting effects:

```cs
	var subConditions = new[] {
		Expr.Create( (Customer c) => c.IsVIP ),
		Expr.Create( (Customer c) => c.IsSpecial ),
		Expr.Create( (Customer c) => c.IsJerk )
	};
	var isNonTrivialCustomer = subConditions.Fold( Expression.OrElse ); // c => c.IsVIP || c.IsSpecial || c.IsJerk
	var isSpecialVIPJerk = subConditions.Fold( Expression.AndAlso ); // c => c.IsVIP && c.IsSpecial && c.IsJerk
```

Or here's a more real-life example:

```cs
	public IQueryable<Customer> GetCustomersInIncomeBrackets( IEnumerable<Range<int>> brackets ) {
		
		var bracketFilters = brackets.Select( b => Expr.Create( (Customer c) => c.Income >= b.Start && c.Income <= b.End );
		var bracketsFilter = bracketFilters.Fold( Expression.OrElse ); 

		// bracketsFilter = c => 
		//	   ( c.Income >= brackets[0].Start && c.Income <= brackets[0].End )
		//	|| ( c.Income >= brackets[1].Start && c.Income <= brackets[1].End )
		//	|| ...
		//	|| ( c.Income >= brackets[n].Start && c.Income <= brackets[n].End )
	
		return Database.Customers.Where( bracketsFilter );
	}
```

## <a name="setter"></a> Obtain a property setter from a property expression

If you have an expression that just returns one property of its argument, you can turn that expression around and make yourself a function which *assigns* that property.

```cs
  var nameProperty = Expr.Create( (Customer c) => c.Name );
  var nameSetter = Expr.SetterFromGetter( nameProperty ).Compile();
  
  var myCustomer = ...;
  nameSetter( myCustomer, "New Name" );
```
