# Juice your .NET Expression Trees for everything they've got!

1. Code reuse: invoke (i.e. embed) expressions within other, bigger expressions.
2. More code reuse: call [specially formatted] static and extension methods within expressions.
3. 

## Code Reuse: Invoke (i.e. embed) expressions within other, bigger expressions.

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

query = from x in SomeTable
        where x.SomeFlag && (x.OtherFlag || name.Call( x ) == name) && !x.AnotherFlag
        select x;

return query.Expand();
```
