using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace erecruit
{
	public static class Expr
	{
		public static readonly Expression<Func<bool, bool>> Not = x => !x;

		public static Expression<Func<T, R>> Create<T, R>( Expression<Func<T, R>> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, R>>>() != null );
			return e;
		}
		public static Expression<Func<T, U, R>> Create<T, U, R>( Expression<Func<T, U, R>> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, U, R>>>() != null );
			return e;
		}
		public static Expression<Func<T, U, V, R>> Create<T, U, V, R>( Expression<Func<T, U, V, R>> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, U, V, R>>>() != null );
			return e;
		}

		/// <summary>
		/// Given a list of values (a, b, c, ... z), returns an expression of the form:
		///		x =&gt; x == a || x == b || x == c || ... || x == z. 
		///	See remarks.
		/// </summary>
		/// <remarks>
		/// From the pure logical point of view, this is just an equivalent of IEnumerable.Contains. The use
		/// of it, however, comes when combined with Entity Framework. EF normally caches compiled queries,
		/// which tremendously speeds up the application (since query compilation may take seconds or even tens
		/// of seconds), but it does not cache queries which contain the IEnumerable.Contains call. To work around
		/// this limitation, we use this method to replace the Contains() calls with multiple conjuncted equivalency
		/// tests, which EF will happily cache.
		/// 
		/// One drawback of this approach is that there will be effectively a different expression tree for
		/// each size of the values list - i.e. "x == a" for list of one element, "x == a || x == b" for list
		/// of two elements, and so on. These expression trees will be treated by EF as different ones (because,
		/// after all, they ARE different), which will cause EF to recompile each of them anew.
		/// To counteract this effect (to some degree), I use the "granularity" parameter to reduce the number
		/// of different expressions produced for different list sizes. The meaning of it is that there will
		/// be the same expression produced for lists of sizes from 1 to "granularity-1", then there will be a
		/// different one produced for lists of sizes from "granularity" to "2*granularity-1", and so on.
		/// Technically, this is achieved by repeating the last element of the list until the list's size
		/// reaches a factor of "granularity".
		/// </remarks>
		public static Expression<Func<T, bool>> EqualToAny<T>( IEnumerable<T> items, int granularity = 10 ) {
			if ( items == null || !items.Any() ) return _ => false;

			granularity = Math.Max( 1, granularity );
			if ( granularity > 1 ) {
				var ii = items.ToList();
				items = ii.Count % granularity == 0 ? ii : ii.Concat( Enumerable.Repeat( ii.Last(), granularity - (ii.Count % granularity) ) );
			}

			var arg = Expression.Parameter( typeof( T ), "x" );
			return Expression.Lambda<Func<T, bool>>(
				items.Select( i => {
					var t = Tuple.Create( i );
					return Expression.Equal( arg, Expression.Property( Expression.Constant( t ), "Item1" ) );
				} )
				.Aggregate( Expression.Or ),
				arg );
		}

		public static string MemberName<T>( this Expression<T> expr )
		{
			Contract.Requires( expr != null );
			return expr.Body.MemberName();
		}

		public static string MemberName( this Expression expr )
		{
			switch ( expr.NodeType )
			{
				case ExpressionType.MemberAccess: return (expr as MemberExpression).Member.Name;
				case ExpressionType.Call: return (expr as MethodCallExpression).Method.Name;
				case ExpressionType.Convert: return (expr as UnaryExpression).Operand.MemberName();
				default: return null;
			}
		}

		public static Expression<Func<T, R>> Compose<T, R, U>( this Expression<Func<T, U>> first, Expression<Func<U, R>> second )
		{
			Contract.Requires( first != null );
			Contract.Requires( second != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, R>>>() != null );
			return Expression.Lambda<Func<T, R>>(
					new VReplaceParameters( second.Parameters.ToDictionary( _ => _, _ => first.Body ) ).Visit( second.Body ),
					first.Parameters );
		}

		public static Expression<Func<T, R>> Compose<T, R, U>( this Expression<Func<T, U>> first, Expression<Func<T, U, R>> second )
		{
			Contract.Requires( first != null );
			Contract.Requires( second != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, R>>>() != null );

			var parameterSubstitutes = new[] 
				{
					new { from = second.Parameters[0], to = first.Parameters[0] as Expression },
					new { from = second.Parameters[1], to = first.Body }
				}
				.ToDictionary( x => x.from, x => x.to );

			return Expression.Lambda<Func<T, R>>( new VReplaceParameters( parameterSubstitutes ).Visit( second.Body ), first.Parameters );
		}

		public static Expression<Func<T, U>> Compose<T, U>( this Expression<Func<T, U>> first,
				IEnumerable<Expression<Func<T, U, U>>> sequence )
		{
			Contract.Requires( first != null );
			Contract.Requires( sequence != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, U>>>() != null );

			return Expression.Lambda<Func<T, U>>(
					sequence.Aggregate(
							first.Body,
							( prev, e ) => new VReplaceParameters( new[] 
								{
									new { a = e.Parameters[0], b = first.Parameters[0] as Expression }, 
									new { a = e.Parameters[1], b = prev }
								}
								.ToDictionary( x => x.a, x => x.b )
							)
							.Visit( e.Body )
					),
					first.Parameters
			);
		}

		public static Expression<Action<T, U>> SetterFromGetter<T, U>( Expression<Func<T, U>> getter ) {
			Contract.Requires( getter != null );
			Contract.Ensures( Contract.Result<Expression<Action<T,U>>>() != null );

			var me = getter.Body as MemberExpression;
			var prop = me == null ? null : me.Member as PropertyInfo;
			if ( prop == null || !prop.CanWrite ) throw new InvalidOperationException( "Expression '" + getter + "' does is not a property reference expression or the referenced property is not writeable." );

			var t = Expression.Parameter( typeof( T ) );
			var fvs = Expression.Parameter( typeof( U ) );
			return Expression.Lambda<Action<T, U>>( Expression.Assign( Expression.Property( t, prop ), fvs ), t, fvs );
		}

		public static Expression ReplaceParameter( this Expression expr, ParameterExpression replaceWhat, Expression replaceWith )
		{
			return expr.ReplaceParameters( new[] { 0 }.ToDictionary( _ => replaceWhat, _ => replaceWith ) );
		}

		public static Expression ReplaceParameters( this Expression expr, IDictionary<ParameterExpression, Expression> mapping )
		{
			return new VReplaceParameters( mapping ).Visit( expr );
		}

		public static ExpressionCastBuilder<T, U> Cast<T, U>( this Expression<Func<T, U>> expr )
		{
			Contract.Requires( expr != null );
			return new ExpressionCastBuilder<T, U>( expr );
		}

		public static Expression<Func<T, U>> Fold<T, U>( this IEnumerable<Expression<Func<T, U>>> exprs, Func<Expression, Expression, Expression> fold )
		{
			Contract.Requires( exprs != null );
			Contract.Requires( fold != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, U>>>() != null );

			var id = Expr.Create( ( T t ) => t );
			return Expression.Lambda<Func<T, U>>(
					exprs.Where( e => e != null ).Select( e => id.Compose( e ).Body ).Aggregate( fold ),
					id.Parameters );
		}

		public static Expression<Func<T, bool>> And<T>( this Expression<Func<T, bool>> a, Expression<Func<T, bool>> b )
		{
			Contract.Requires( a != null );
			Contract.Requires( b != null );
			Contract.Ensures( Contract.Result<Expression<Func<T,bool>>>() != null );
			return new[] { a, b }.Fold( Expression.AndAlso );
		}

		public static Expression<Func<T, bool>> Or<T>( this Expression<Func<T, bool>> a, Expression<Func<T, bool>> b )
		{
			Contract.Requires( a != null );
			Contract.Requires( b != null );
			Contract.Ensures( Contract.Result<Expression<Func<T, bool>>>() != null );
			return new[] { a, b }.Fold( Expression.OrElse );
		}

		public static IDictionary<string, object> CallParameters<T>( this Expression<T> expr )
		{
			Contract.Requires( expr != null );
			Contract.Ensures( Contract.Result<IDictionary<string, object>>() != null );
			return expr.Body.CallParameters();
		}

		public static IDictionary<string, object> CallParameters( this Expression expr )
		{
			Contract.Requires( expr != null );
			Contract.Ensures( Contract.Result<IDictionary<string, object>>() != null );

			switch ( expr.NodeType )
			{
				case ExpressionType.Call:
					{
						var mce = expr as MethodCallExpression;
						return
								mce.Arguments
								.Zip( mce.Method.GetParameters(), ( a, p ) => new { param = p, value = a.Eval() } )
								.ToDictionary( x => x.param.Name, x => x.value );
					}

				case ExpressionType.Convert: return (expr as UnaryExpression).Operand.CallParameters();
				default: throw new InvalidOperationException( "Unsupported expression type: " + expr.NodeType );
			}
		}

		public static object Eval( this Expression expression )
		{
			return
					Expression.Lambda<Func<object>>(
							Expression.Convert( expression, typeof( object ) )
					)
					.Compile()
					();
		}

		class VReplaceParameters : ExpressionVisitorWithDepthCheck
		{
			private readonly IDictionary<ParameterExpression, Expression> _substitutes;
			public VReplaceParameters( IDictionary<ParameterExpression, Expression> substitutes ) { _substitutes = substitutes; }

			protected override Expression VisitParameter( ParameterExpression node )
			{
				Expression result;
				return _substitutes.TryGetValue( node, out result ) ? result : node;
			}
		}

		public struct ExpressionCastBuilder<T, U>
		{
			private readonly Expression<Func<T, U>> _source;
			public ExpressionCastBuilder( Expression<Func<T, U>> source )
			{
				Contract.Requires( source != null );
				_source = source;
			}
			public Expression<Func<T, X>> As<X>()
			{
				Contract.Ensures( Contract.Result<Expression<Func<T, X>>>() != null );
				return Expression.Lambda<Func<T, X>>( Expression.Convert( _source.Body, typeof( X ) ), _source.Parameters );
			}
		}

		/// <summary>
		/// ExpressionVisitor (used by methods in this library) is recursive so it could cause stack overflow with very deep expression trees
		/// Also, even if some large expressions would be succesfully expanded, there might be more problems later with them 
		/// (eg. when converted to SQL it might result in statement having more that maximum allowed number of parameters)
		/// Limit here was established after testing the Expand method with default stack size for IIS process (256KB for 32bit and 512KB for 64bit).
		/// Running with larger stack size or release version should allow for more recursive iterations. Also, the validation itself wastes some stack, so removing it might allow more iterations, at the risk of not having protection against stack overflow.
		/// NOTE: System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack() might work instead of this on some platforms
		/// </summary>
		public static int MaxRecursionDepth = 1500;

		internal abstract class ExpressionVisitorWithDepthCheck : System.Linq.Expressions.ExpressionVisitor
		{
			private int _currRecursionDepth = 0;

			public override Expression Visit(Expression node)
			{
				_currRecursionDepth++;
				if (_currRecursionDepth > MaxRecursionDepth)
				{
					throw new InvalidOperationException($"Maximum recursion depth ({MaxRecursionDepth}) exceeded. This happens when expression tree has too many nested nodes.");
				}
				try
				{
					return base.Visit(node);
				}
				finally
				{
					_currRecursionDepth--;
				}
			}
		}
	}
}