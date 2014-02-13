using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace erecruit.Utils
{
	// TODO: [fs] add Call/Expand support for two and three args
	public static class ExpressionNesting
	{
		public static TResult Call<TInput, TResult>( this Expression<Func<TInput, TResult>> expr, TInput arg ) {
			throw new NotSupportedException( "This method is not intended to be actually executed. It is supposed to be used as part of an expression, in combination with the Expression.Expand() method." );
		}

		public static Expression<T> Expand<T>( this Expression<T> expr ) {
			return Expression.Lambda<T>( expr.Body.Expand(), expr.Parameters );
		}

		public static Expression Expand( this Expression expr ) {
			return new V().Visit( expr );
		}

		public static IQueryable<T> Expand<T>( this IQueryable<T> q ) {
			return q.Provider.CreateQuery<T>( q.Expression.Expand() );
		}

		class V : ExpressionVisitor
		{
			static readonly MethodInfo _callMethod = new Func<Expression<Func<int, int>>, int, int>( Call<int, int> ).GetMethodInfo().GetGenericMethodDefinition();

			protected override Expression VisitMethodCall( MethodCallExpression node ) {
				if ( node.Method.IsGenericMethod && node.Method.GetGenericMethodDefinition() == _callMethod ) {
					var e = DigOutConstant( node.Arguments[0] ) as LambdaExpression;
					if ( e == null ) throw new InvalidOperationException( "Cannot expand expression '" + node + "'. The first ('this') argument must be a constant (i.e. value of a property or a variable) of type Expression<T> and not null." );

					return base.Visit( e.Body.ReplaceParameter( e.Parameters[0], node.Arguments[1] ) );
				}
				else {
					var sbs = GetSubstituteFor( node.Method );
					if ( sbs != null ) {
						return base.Visit( sbs.Body.ReplaceParameters(
							sbs.Parameters
							.Zip( node.Arguments, ( p, a ) => new { p, a } )
							.ToDictionary( x => x.p, x => x.a ) ) );
					}
				}

				return base.VisitMethodCall( node );
			}

			object DigOutConstant( Expression e ) {
				var c = e as ConstantExpression;
				var m = e as MemberExpression;
				if ( c != null ) return c.Value;
				if ( m != null ) {
					var obj = DigOutConstant( m.Expression );
					if ( obj == null ) return null;

					var getValue = _memberGetters.GetOrAdd( m.Member, member => {
						var param = Expression.Parameter( typeof( object ) );
						return Expression.Lambda<Func<object, object>>(
							Expression.Convert(
								Expression.MakeMemberAccess(
									Expression.Convert( param, member.DeclaringType ),
									member
								),
								typeof( object )
							),
							param
						)
						.Compile();
					} );
					return getValue( obj );
				}

				return null;
			}

			static LambdaExpression GetSubstituteFor( MethodInfo m ) {
				return _expressionSubstitutes.GetOrAdd( m, x => {
					var s = x.GetCustomAttributes( typeof( SubstituteAttribute ), false ).Any();
					if ( !s ) return null;
					if ( !x.IsStatic ) throw new InvalidOperationException( "Only static methods can be [Substitute]d." );

					var sink = new SubstSink();
					using ( Subst.SetSink( sink ) ) {
						try { x.Invoke( null, x.GetParameters().Select( p => !p.ParameterType.IsByRef ? Activator.CreateInstance( p.ParameterType ) : null ).ToArray() ); }
						catch { }
						if ( sink.Expr == null ) throw new InvalidOperationException( "A method that is [Substitute]d must have a body wholly consisting of a call to Subst.Expr." );
						if ( x.GetParameters().Any( p => p.IsOut ) ) throw new InvalidOperationException( "A method that is [Substitute]d cannot have 'out' or 'ref' parameters." );
						if ( !sink.Expr.Parameters.Select( p => p.Type ).SequenceEqual( x.GetParameters().Select( p => p.ParameterType ) )
							|| sink.Expr.ReturnType != x.ReturnType ) {
							throw new InvalidOperationException( "The argument passed to Subst.Expr must have the same number and types of parameters and the same return type as the enclosing static method." );
						}

						return sink.Expr;
					}
				} );
			}

			class SubstSink : ISubstSink
			{
				public LambdaExpression Expr;
				void ISubstSink.Expr( LambdaExpression expr ) { Expr = expr; }
			}

			static ConcurrentDictionary<MemberInfo, Func<object, object>> _memberGetters = new ConcurrentDictionary<MemberInfo, Func<object, object>>();
			static ConcurrentDictionary<MethodInfo, LambdaExpression> _expressionSubstitutes = new ConcurrentDictionary<MethodInfo, LambdaExpression>();
		}
	}

	[AttributeUsage( AttributeTargets.Method )]
	public class SubstituteAttribute : Attribute { }

	public static class Subst
	{
		[DebuggerStepThrough]
		public static R Expr<R>( Expression<Func<R>> expr ) { Sink( s => s.Expr( expr ) ); return default( R ); }

		[DebuggerStepThrough]
		public static R Expr<T, R>( Expression<Func<T, R>> expr ) { Sink( s => s.Expr( expr ) ); return default( R ); }

		[DebuggerStepThrough]
		public static R Expr<T1, T2, R>( Expression<Func<T1, T2, R>> expr ) { Sink( s => s.Expr( expr ) ); return default( R ); }

		[DebuggerStepThrough]
		public static R Expr<T1, T2, T3, R>( Expression<Func<T1, T2, T3, R>> expr ) { Sink( s => s.Expr( expr ) ); return default( R ); }

		[DebuggerStepThrough]
		public static R Expr<T1, T2, T3, T4, R>( Expression<Func<T1, T2, T3, T4, R>> expr ) { Sink( s => s.Expr( expr ) ); return default( R ); }

		[ThreadStatic]
		static LinkedList<ISubstSink> _sinks;

		public static IDisposable SetSink( ISubstSink sink ) {
			if ( _sinks == null ) _sinks = new LinkedList<ISubstSink>();
			var node = _sinks.AddLast( sink );
			return new NodeRemoveDisposable( node );
		}

		[DebuggerStepThrough]
		static void Sink( Action<ISubstSink> s ) {
			var sink = _sinks == null || _sinks.Last == null ? null : _sinks.Last.Value;
			if ( sink != null ) s( sink );
			else throw new InvalidOperationException( "This method is not supposed to be actually invoked. It is supposed to be used as part of an expression tree and with combination with the Expression.Expand extension method." );
		}

		class NodeRemoveDisposable : IDisposable
		{
			readonly LinkedListNode<ISubstSink> _node;
			public NodeRemoveDisposable( LinkedListNode<ISubstSink> node ) {
				_node = node;
			}
			public void Dispose() {
				_node.List.Remove( _node );
			}
		}
	}

	public interface ISubstSink
	{
		void Expr( LambdaExpression expr );
	}
}