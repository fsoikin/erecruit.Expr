using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace erecruit.Tests
{
	public class ExpressionNesting
	{
		[Fact]
		public void Should_expand_simple_call()
		{
			var e = Expr.Create( ( int a ) => a + 1 );
			var x = Expr.Create( ( string s ) => e.Call( s.Length ) );
			var c = x.Expand();
			Assert.Equal( 3, c.Compile()( "ab" ) );
		}

		[Fact]
		public void Unexpanded_expression_should_throw_exception_if_compiled()
		{
			var e = Expr.Create( ( int a ) => a + 1 );
			var x = Expr.Create( ( string s ) => e.Call( s.Length ) );
			Assert.Throws<NotSupportedException>( () => x.Compile()( "ab" ) );
		}

		[Fact]
		public void Should_expand_nested_call()
		{
			var e = Expr.Create( ( string a ) => a.Length );
			var x = Expr.Create( ( int[] ii, string s ) => ii.Contains( e.Call( s ) ) );
			var c = x.Expand();
			Assert.Equal( true, c.Compile()( new[] { 1, 2 }, "ab" ) );
			Assert.Equal( false, c.Compile()( new[] { 1, 3, 4 }, "ab" ) );
		}

		[Fact]
		public void Should_expand_doubly_nested_call()
		{
			var e1 = Expr.Create( ( string a ) => a.Length );
			var e2 = Expr.Create( ( string a ) => a + "1" );
			var x = Expr.Create( ( int[] ii, string s ) => ii.Contains( e1.Call( e2.Call( s ) ) ) );
			var c = x.Expand();
			Assert.Equal( true, c.Compile()( new[] { 1, 3 }, "ab" ) );
			Assert.Equal( false, c.Compile()( new[] { 1, 5, 4 }, "ab" ) );
		}

		[Fact]
		public void Should_expand_call_nested_inside_inner_quotation()
		{
			var e = Expr.Create( ( string a ) => a.Length );
			var x = Expr.Create( ( string[] ii ) => ii.Any( s => e.Call( s ) == 5 ) );
			var c = x.Expand();
			Assert.Equal( false, c.Compile()( new[] { "a", "ab" } ) );
			Assert.Equal( true, c.Compile()( new[] { "a", "ab", "12345" } ) );
		}

		[Fact]
		public void Should_expand_call_of_expression_that_itself_contains_a_call()
		{
			var e1 = Expr.Create( ( string a ) => a.Length );
			var e2 = Expr.Create( ( string a ) => e1.Call( a ) == 5 );
			var x = Expr.Create( ( string[] ii ) => ii.Any( s => e2.Call( s ) ) );
			var c = x.Expand();
			Assert.Equal( false, c.Compile()( new[] { "a", "ab" } ) );
			Assert.Equal( true, c.Compile()( new[] { "a", "ab", "12345" } ) );
		}

		[Fact]
		public void Should_expand_call_nested_inside_inner_quotation_that_does_not_contain_anything_else_besides_that_call()
		{
			var e = Expr.Create( ( string a ) => a.Length == 5 );
			var x = Expr.Create( ( string[] ii ) => ii.Any( s => e.Call( s ) ) );
			var c = x.Expand();
			Assert.Equal( false, c.Compile()( new[] { "a", "ab" } ) );
			Assert.Equal( true, c.Compile()( new[] { "a", "ab", "12345" } ) );
		}
	}
}