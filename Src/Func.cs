using System;
using System.Diagnostics.Contracts;

namespace erecruit
{
	public static class Func
	{
		public static Func<T> Create<T>( Func<T> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Func<T>>() != null );
			return e;
		}
		public static Func<T, R> Create<T, R>( Func<T, R> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Func<T, R>>() != null );
			return e;
		}
		public static Func<T, R> Create<T, R>( T argumentInstance, Func<T, R> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Func<T, R>>() != null );
			return e;
		}
		public static Func<T, U, R> Create<T, U, R>( Func<T, U, R> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Func<T, U, R>>() != null );
			return e;
		}
		public static Func<T, U, V, R> Create<T, U, V, R>( Func<T, U, V, R> e )
		{
			Contract.Requires( e != null );
			Contract.Ensures( Contract.Result<Func<T, U, V, R>>() != null );
			return e;
		}
	}
}