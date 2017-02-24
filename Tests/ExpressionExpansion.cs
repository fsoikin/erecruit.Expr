using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace erecruit.Tests
{
	public class ExpressionExpansion
	{
		[Fact]
		public void Should_expand_simple_call_with_one_argument()
		{
			var e1 = Expr.Create( ( A a ) => ToB( a ) );
			var e2 = Expr.Create( ( A a ) => new B { L = a.S.Length } );
			Assert.Equal( e1.Expand().ToString(), e2.ToString() );
		}

		[Fact]
		public void Should_expand_simple_call_with_two_arguments()
		{
			var e1 = Expr.Create( ( A a ) => ToB( a, 1 ) );
			var e2 = Expr.Create( ( A a ) => new B { L = a.S.Length + 1 } );
			Assert.Equal( e1.Expand().ToString(), e2.ToString() );
		}

		[Fact]
		public void Should_expand_simple_call_with_three_arguments()
		{
			var e1 = Expr.Create( ( A a ) => ToB( a, 1, "xy" ) );
			var e2 = Expr.Create( ( A a ) => new B { L = (a.S + "xy").Length + 1 } );
			Assert.Equal( e1.Expand().ToString(), e2.ToString() );
		}

		[Fact]
		public void Should_throw_when_invoked_without_expansion()
		{
			var e1 = Expr.Create( ( A a ) => ToB( a ) );
			Assert.Throws<InvalidOperationException>( () => e1.Compile()( new A { S = "a" } ) );
		}

		[Fact]
		public void Should_not_expand_methods_not_marked_with_Substitute_attribute()
		{
			var e1 = Expr.Create( ( A a ) => ToB_NoAttribute( a ) );
			Assert.Equal( e1.Expand().ToString(), e1.ToString() );
		}

		[Fact]
		public void Should_throw_when_target_method_does_not_call_Subst_Expr()
		{
			var e1 = Expr.Create( ( A a ) => ToB_NoSink( a ) );
			Assert.Throws<Exception>( () => e1.Expand() );
		}

		[Fact]
		public void Should_throw_when_expression_has_fewer_args_than_the_enclosing_method()
		{
			var e1 = Expr.Create( ( A a ) => ToB_FewerArgs( a ) );
			Assert.Throws<InvalidOperationException>( () => e1.Expand() );
		}

		[Fact]
		public void Should_throw_when_expression_has_more_args_than_the_enclosing_method()
		{
			var e1 = Expr.Create( ( A a ) => ToB_MoreArgs( a ) );
			Assert.Throws<InvalidOperationException>( () => e1.Expand() );
		}

		[Fact]
		public void Should_throw_when_expression_has_args_of_types_different_from_those_of_the_enclosing_methods_args()
		{
			var e1 = Expr.Create( ( A a ) => ToB_WrongTypeOfArgs( a ) );
			Assert.Throws<InvalidOperationException>( () => e1.Expand() );
		}

		[Fact]
		public void Should_throw_when_expression_return_type_is_different_from_the_enclosing_method()
		{
			var e1 = Expr.Create( ( A a ) => ToB_WrongReturnType( a ) );
			Assert.Throws<InvalidOperationException>( () => e1.Expand() );
		}

		[Fact]
		public void Should_not_cause_stack_overflow_with_expression_tree_too_deep()
		{
			const int batchSize = 500;
			Func<int, bool> testExpand = size =>
			{
				var items = Enumerable.Range(0, size);
				var equalToAnyExpr = Expr.EqualToAny(items);
				var e1 = Expr.Create((int num) => equalToAnyExpr.Call(num));
				try
				{
					e1.Expand();
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			};
			int stackSize = ((IntPtr.Size == 8) ? 512 : 256) * 1024;//simulate minimum stack size in most environments: 256KB for 32bit process, 512KB for 64bit process. Assumption here is that test environment matches deployment.
			Func<int, bool> testExpandWithMinStackSize = size =>
			{
				bool result = false;
				Exception caughtEx = null;
				var expandThread = new System.Threading.Thread(() =>
				{
					try
					{
						result = testExpand(size);
					}
					catch (Exception ex)
					{
						caughtEx = ex;
					}
				}, stackSize);
				expandThread.Start();
				expandThread.Join();
				if (caughtEx != null)
					throw caughtEx;
				else
					return result;
			};
			var results = Enumerable.Range(0, 20).Select(idx => testExpandWithMinStackSize(idx * batchSize)).ToArray();
			var errIdx = Array.IndexOf(results, false);//find first failure
			//confirm it succeeds for first few sizes, and then fails after some limit was reached
			Assert.True(errIdx > 0);
			Assert.All(results.Take(errIdx), result => Assert.True(result));
			Assert.All(results.Skip(errIdx), result => Assert.False(result));
		}

		class A
		{
			public string S { get; set; }
		}

		class B
		{
			public int L { get; set; }
		}

		[Substitute]
		static B ToB( A a_ ) { return Subst.Expr( ( A a ) => new B { L = a.S.Length } ); }

		[Substitute]
		static B ToB( A a_, int n_ ) { return Subst.Expr( ( A a, int n ) => new B { L = a.S.Length + n } ); }

		[Substitute]
		static B ToB( A a_, int n_, string s_ ) { return Subst.Expr( ( A a, int n, string s ) => new B { L = (a.S + s).Length + n } ); }

		static B ToB_NoAttribute( A a ) { return new B { L = a.S.Length }; }
		[Substitute] static B ToB_NoSink( A a ) { return new B { L = a.S.Length }; }
		[Substitute] static B ToB_MoreArgs( A a ) { return Subst.Expr( ( A aa, int n ) => new B() ); }
		[Substitute] static B ToB_FewerArgs( A a ) { return Subst.Expr( () => new B() ); }
		[Substitute] static B ToB_WrongTypeOfArgs( A a ) { return Subst.Expr( ( B b ) => new B() ); }
		[Substitute] static B ToB_WrongReturnType( A a ) { Subst.Expr( ( A b ) => new A() ); return null; }
	}
}