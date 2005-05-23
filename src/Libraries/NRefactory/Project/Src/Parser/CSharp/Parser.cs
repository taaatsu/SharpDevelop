
#line  1 "cs.ATG" 
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using ICSharpCode.NRefactory.Parser;
using ICSharpCode.NRefactory.Parser.AST;
/*
  Parser.frame file for NRefactory.
 */
using System;
using System.Reflection;

namespace ICSharpCode.NRefactory.Parser.CSharp {



internal class Parser : AbstractParser
{
	const int maxT = 124;

	const  bool   T            = true;
	const  bool   x            = false;
	

#line  13 "cs.ATG" 
string        assemblyName     = null;
StringBuilder qualidentBuilder = new StringBuilder();

public string ContainingAssembly {
	set {
		assemblyName = value;
	}
}

Token t {
	get {
		return lexer.Token;
	}
}

Token la {
	get {
		return lexer.LookAhead;
	}
}

public void Error(string s)
{
	if (errDist >= minErrDist) {
		errors.Error(la.line, la.col, s);
	}
	errDist = 0;
}

public override Expression ParseExpression()
{
	Expression expr;
	Expr(out expr);
	return expr;
}
// Begin ISTypeCast
bool IsTypeCast()
{
	if (la.kind != Tokens.OpenParenthesis) {
		return false;
	}
	if (IsSimpleTypeCast()) {
		return true;
	}
	return GuessTypeCast();
}

// "(" ( typeKW [ "[" {","} "]" | "*" ] | void  ( "[" {","} "]" | "*" ) ) ")"
bool IsSimpleTypeCast ()
{
	// assert: la.kind == _lpar
	lexer.StartPeek();
	Token pt1 = lexer.Peek();
	Token pt  = lexer.Peek();
	
	if (Tokens.TypeKW[pt1.kind]) {
		if (pt.kind == Tokens.Times || pt.kind == Tokens.OpenSquareBracket) {
			return IsPointerOrDims(ref pt) && pt.kind == Tokens.CloseParenthesis;
		} else {
		  return pt.kind == Tokens.CloseParenthesis;
		}
	} else if (pt1.kind == Tokens.Void) {
		return IsPointerOrDims(ref pt) && pt.kind == Tokens.CloseParenthesis;
	}
	
	return false;
}

// "(" NonGenericTypeName ")" castFollower
// NonGenericTypeName = ident [ "::" ident ] {  "." ident }
bool GuessTypeCast ()
{
	// assert: la.kind == _lpar
	StartPeek();
	Token pt = Peek();

	// ident
	if (pt.kind != Tokens.Identifier) {
		return false;
	}	
	pt = Peek();
	// "::" ident
	if (pt.kind == Tokens.DoubleColon) {
		pt = Peek();
		if (pt.kind != Tokens.Identifier) {
			return false;
		}
		pt = Peek();
	}
	// { "." ident }
	while (pt.kind == Tokens.Dot) {
		pt = Peek();
		if (pt.kind != Tokens.Identifier) {
			return false;
		}
		pt = Peek();
	}
	// ")"
  if (pt.kind != Tokens.CloseParenthesis) {
  	return false;
  }
  // check successor
  pt = Peek();
  return Tokens.CastFollower[pt.kind] || (Tokens.TypeKW[pt.kind] && lexer.Peek().kind == Tokens.Dot);
}
// END IsTypeCast

/* Checks whether the next sequences of tokens is a qualident *
 * and returns the qualident string                           */
/* !!! Proceeds from current peek position !!! */
bool IsQualident(ref Token pt, out string qualident)
{
	if (pt.kind == Tokens.Identifier) {
		qualidentBuilder.Length = 0; qualidentBuilder.Append(pt.val);
		pt = Peek();
		while (pt.kind == Tokens.Dot) {
			pt = Peek();
			if (pt.kind != Tokens.Identifier) {
				qualident = String.Empty;
				return false;
			}
			qualidentBuilder.Append('.');
			qualidentBuilder.Append(pt.val);
			pt = Peek();
		}
		qualident = qualidentBuilder.ToString();
		return true;
	}
	qualident = String.Empty;
	return false;
}

/* Skips generic type extensions */
/* !!! Proceeds from current peek position !!! */

/* skip: { "*" | "[" { "," } "]" } */
/* !!! Proceeds from current peek position !!! */
bool IsPointerOrDims (ref Token pt)
{
	for (;;) {
		if (pt.kind == Tokens.OpenSquareBracket) {
			do pt = Peek();
			while (pt.kind == Tokens.Comma);
			if (pt.kind != Tokens.CloseSquareBracket) return false;
		} else if (pt.kind != Tokens.Times) break;
		pt = Peek();
	}
	return true;
}

/* Return the n-th token after the current lookahead token */
void StartPeek()
{
	lexer.StartPeek();
}

Token Peek()
{
	return lexer.Peek();
}

Token Peek (int n)
{
	lexer.StartPeek();
	Token x = la;
	while (n > 0) {
		x = lexer.Peek();
		n--;
	}
	return x;
}

/*-----------------------------------------------------------------*
 * Resolver routines to resolve LL(1) conflicts:                   *                                                  *
 * These resolution routine return a boolean value that indicates  *
 * whether the alternative at hand shall be choosen or not.        *
 * They are used in IF ( ... ) expressions.                        *       
 *-----------------------------------------------------------------*/

/* True, if ident is followed by "=" */
bool IdentAndAsgn ()
{
	return la.kind == Tokens.Identifier && Peek(1).kind == Tokens.Assign;
}

bool IsAssignment () { return IdentAndAsgn(); }

/* True, if ident is followed by ",", "=", or ";" */
bool IdentAndCommaOrAsgnOrSColon () {
	int peek = Peek(1).kind;
	return la.kind == Tokens.Identifier && 
	       (peek == Tokens.Comma || peek == Tokens.Assign || peek == Tokens.Semicolon);
}
bool IsVarDecl () { return IdentAndCommaOrAsgnOrSColon(); }

/* True, if the comma is not a trailing one, *
 * like the last one in: a, b, c,            */
bool NotFinalComma () {
	int peek = Peek(1).kind;
	return la.kind == Tokens.Comma &&
	       peek != Tokens.CloseCurlyBrace && peek != Tokens.CloseSquareBracket;
}

/* True, if "void" is followed by "*" */
bool NotVoidPointer () {
	return la.kind == Tokens.Void && Peek(1).kind != Tokens.Times;
}

/* True, if "checked" or "unchecked" are followed by "{" */
bool UnCheckedAndLBrace () {
	return la.kind == Tokens.Checked || la.kind == Tokens.Unchecked &&
	       Peek(1).kind == Tokens.OpenCurlyBrace;
}

/* True, if "." is followed by an ident */
bool DotAndIdent () {
	return la.kind == Tokens.Dot && Peek(1).kind == Tokens.Identifier;
}

/* True, if ident is followed by ":" */
bool IdentAndColon () {
	return la.kind == Tokens.Identifier && Peek(1).kind == Tokens.Colon;
}

bool IsLabel () { return IdentAndColon(); }

/* True, if ident is followed by "(" */
bool IdentAndLPar () {
	return la.kind == Tokens.Identifier && Peek(1).kind == Tokens.OpenParenthesis;
}

/* True, if "catch" is followed by "(" */
bool CatchAndLPar () {
	return la.kind == Tokens.Catch && Peek(1).kind == Tokens.OpenParenthesis;
}
bool IsTypedCatch () { return CatchAndLPar(); }

/* True, if "[" is followed by the ident "assembly" */
bool IsGlobalAttrTarget () {
	Token pt = Peek(1);
	return la.kind == Tokens.OpenSquareBracket && 
	       pt.kind == Tokens.Identifier && pt.val == "assembly";
}

/* True, if "[" is followed by "," or "]" */
bool LBrackAndCommaOrRBrack () {
	int peek = Peek(1).kind;
	return la.kind == Tokens.OpenSquareBracket &&
	       (peek == Tokens.Comma || peek == Tokens.CloseSquareBracket);
}

bool IsDims () { return LBrackAndCommaOrRBrack(); }

/* True, if "[" is followed by "," or "]" *
 * or if the current token is "*"         */
bool TimesOrLBrackAndCommaOrRBrack () {
	return la.kind == Tokens.Times || LBrackAndCommaOrRBrack();
}
bool IsPointerOrDims () { return TimesOrLBrackAndCommaOrRBrack(); }
bool IsPointer () { return la.kind == Tokens.Times; }


bool SkipGeneric(ref Token pt)
{
	if (pt.kind == Tokens.LessThan) {
		int braces = 1;
		while (braces != 0) {
			pt = Peek();
			if (pt.kind == Tokens.GreaterThan) {
				--braces;
			} else if (pt.kind == Tokens.LessThan) {
				++braces;
			} else if (pt.kind == Tokens.Semicolon || pt.kind == Tokens.OpenCurlyBrace || pt.kind == Tokens.CloseCurlyBrace || pt.kind == Tokens.EOF) {
				return false;
			}
		}
		pt = Peek();
		return true;
	}
	return true;
}
/* True, if lookahead is a primitive type keyword, or *
 * if it is a type declaration followed by an ident   */
bool IsLocalVarDecl () {
	if (IsYieldStatement()) {
		return false;
	}
	if ((Tokens.TypeKW[la.kind] && Peek(1).kind != Tokens.Dot) || la.kind == Tokens.Void) {
		return true;
	}
	
	StartPeek();
	Token pt = la ;
	string ignore;
	
	return IsQualident(ref pt, out ignore) && SkipGeneric(ref pt) && IsPointerOrDims(ref pt) && 
	       pt.kind == Tokens.Identifier;
}

/* True if lookahead is type parameters (<...>) followed by the specified token */
bool IsGenericFollowedBy(int token)
{
	Token t = la;
	if (t.kind != Tokens.LessThan) return false;
	StartPeek();
	return SkipGeneric(ref t) && t.kind == token;
}

/* True, if lookahead ident is "where" */
bool IdentIsWhere () {
	return la.kind == Tokens.Identifier && la.val == "where";
}

/* True, if lookahead ident is "get" */
bool IdentIsGet () {
	return la.kind == Tokens.Identifier && la.val == "get";
}

/* True, if lookahead ident is "set" */
bool IdentIsSet () {
	return la.kind == Tokens.Identifier && la.val == "set";
}

/* True, if lookahead ident is "add" */
bool IdentIsAdd () {
	return la.kind == Tokens.Identifier && la.val == "add";
}

/* True, if lookahead ident is "remove" */
bool IdentIsRemove () {
	return la.kind == Tokens.Identifier && la.val == "remove";
}

bool IsNotYieldStatement () {
	return !IsYieldStatement();
}
/* True, if lookahead ident is "yield" and than follows a break or return */
bool IsYieldStatement () {
	return la.kind == Tokens.Identifier && la.val == "yield" && (Peek(1).kind == Tokens.Return || Peek(1).kind == Tokens.Break);
}

/* True, if lookahead is a local attribute target specifier, *
 * i.e. one of "event", "return", "field", "method",         *
 *             "module", "param", "property", or "type"      */
bool IsLocalAttrTarget () {
	int cur = la.kind;
	string val = la.val;

	return (cur == Tokens.Event || cur == Tokens.Return ||
	        (cur == Tokens.Identifier &&
	         (val == "field" || val == "method"   || val == "module" ||
	          val == "param" || val == "property" || val == "type"))) &&
	       Peek(1).kind == Tokens.Colon;
}

bool IsShiftRight() 
{
	Token next = Peek(1);
	// TODO : Add col test (seems not to work, lexer bug...) :  && la.col == next.col - 1
	return (la.kind == Tokens.GreaterThan && next.kind == Tokens.GreaterThan);
}

bool IsTypeReferenceExpression(Expression expr)
{
	if (expr is TypeReferenceExpression) return ((TypeReferenceExpression)expr).TypeReference.GenericTypes.Count == 0;
	while (expr is FieldReferenceExpression) {
		expr = ((FieldReferenceExpression)expr).TargetObject;
	}
	return expr is IdentifierExpression;
}

TypeReferenceExpression GetTypeReferenceExpression(Expression expr, List<TypeReference> genericTypes)
{
	TypeReferenceExpression	tre = expr as TypeReferenceExpression;
	if (tre != null) {
		return new TypeReferenceExpression(new TypeReference(tre.TypeReference.Type, tre.TypeReference.PointerNestingLevel, tre.TypeReference.RankSpecifier, genericTypes));
	}
	StringBuilder b = new StringBuilder();
	WriteFullTypeName(b, expr);
	return new TypeReferenceExpression(new TypeReference(b.ToString(), 0, null, genericTypes));
}

void WriteFullTypeName(StringBuilder b, Expression expr)
{
	FieldReferenceExpression fre = expr as FieldReferenceExpression;
	if (fre != null) {
		WriteFullTypeName(b, fre.TargetObject);
		b.Append('.');
		b.Append(fre.FieldName);
	} else if (expr is IdentifierExpression) {
		b.Append(((IdentifierExpression)expr).Identifier);
	}
}

/*------------------------------------------------------------------------*
 *----- LEXER TOKEN LIST  ------------------------------------------------*
 *------------------------------------------------------------------------*/

/* START AUTOGENERATED TOKENS SECTION */


/*

*/

	void CS() {

#line  552 "cs.ATG" 
		compilationUnit = new CompilationUnit(); 
		while (la.kind == 119) {
			UsingDirective();
		}
		while (
#line  555 "cs.ATG" 
IsGlobalAttrTarget()) {
			GlobalAttributeSection();
		}
		while (StartOf(1)) {
			NamespaceMemberDecl();
		}
		Expect(0);
	}

	void UsingDirective() {

#line  562 "cs.ATG" 
		string qualident = null, aliasident = null;
		
		Expect(119);

#line  565 "cs.ATG" 
		Point startPos = t.Location; 
		if (
#line  566 "cs.ATG" 
IsAssignment()) {
			lexer.NextToken();

#line  566 "cs.ATG" 
			aliasident = t.val; 
			Expect(3);
		}
		Qualident(
#line  567 "cs.ATG" 
out qualident);
		Expect(11);

#line  569 "cs.ATG" 
		if (qualident != null && qualident.Length > 0) {
		 INode node;
		 if (aliasident != null) {
		     node = new UsingDeclaration(aliasident, qualident);
		 } else {
		     node = new UsingDeclaration(qualident);
		 }
		 node.StartLocation = startPos;
		 node.EndLocation   = t.EndLocation;
		 compilationUnit.AddChild(node);
		}
		
	}

	void GlobalAttributeSection() {
		Expect(17);

#line  586 "cs.ATG" 
		Point startPos = t.Location; 
		Expect(1);

#line  586 "cs.ATG" 
		if (t.val != "assembly") Error("global attribute target specifier (\"assembly\") expected");
		string attributeTarget = t.val;
		ArrayList attributes = new ArrayList();
		ICSharpCode.NRefactory.Parser.AST.Attribute attribute;
		
		Expect(9);
		Attribute(
#line  591 "cs.ATG" 
out attribute);

#line  591 "cs.ATG" 
		attributes.Add(attribute); 
		while (
#line  592 "cs.ATG" 
NotFinalComma()) {
			Expect(13);
			Attribute(
#line  592 "cs.ATG" 
out attribute);

#line  592 "cs.ATG" 
			attributes.Add(attribute); 
		}
		if (la.kind == 13) {
			lexer.NextToken();
		}
		Expect(18);

#line  594 "cs.ATG" 
		AttributeSection section = new AttributeSection(attributeTarget, attributes);
		section.StartLocation = startPos;
		section.EndLocation = t.EndLocation;
		compilationUnit.AddChild(section);
		
	}

	void NamespaceMemberDecl() {

#line  678 "cs.ATG" 
		AttributeSection section;
		ArrayList attributes = new ArrayList();
		Modifiers m = new Modifiers();
		string qualident;
		
		if (la.kind == 86) {
			lexer.NextToken();

#line  684 "cs.ATG" 
			Point startPos = t.Location; 
			Qualident(
#line  685 "cs.ATG" 
out qualident);

#line  685 "cs.ATG" 
			INode node =  new NamespaceDeclaration(qualident);
			node.StartLocation = startPos;
			compilationUnit.AddChild(node);
			compilationUnit.BlockStart(node);
			
			Expect(15);
			while (la.kind == 119) {
				UsingDirective();
			}
			while (StartOf(1)) {
				NamespaceMemberDecl();
			}
			Expect(16);
			if (la.kind == 11) {
				lexer.NextToken();
			}

#line  694 "cs.ATG" 
			node.EndLocation   = t.EndLocation;
			compilationUnit.BlockEnd();
			
		} else if (StartOf(2)) {
			while (la.kind == 17) {
				AttributeSection(
#line  698 "cs.ATG" 
out section);

#line  698 "cs.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(3)) {
				TypeModifier(
#line  699 "cs.ATG" 
m);
			}
			TypeDecl(
#line  700 "cs.ATG" 
m, attributes);
		} else SynErr(125);
	}

	void Qualident(
#line  816 "cs.ATG" 
out string qualident) {
		Expect(1);

#line  818 "cs.ATG" 
		qualidentBuilder.Length = 0; qualidentBuilder.Append(t.val); 
		while (
#line  819 "cs.ATG" 
DotAndIdent()) {
			Expect(14);
			Expect(1);

#line  819 "cs.ATG" 
			qualidentBuilder.Append('.');
			qualidentBuilder.Append(t.val); 
			
		}

#line  822 "cs.ATG" 
		qualident = qualidentBuilder.ToString(); 
	}

	void Attribute(
#line  601 "cs.ATG" 
out ICSharpCode.NRefactory.Parser.AST.Attribute attribute) {

#line  602 "cs.ATG" 
		string qualident; 
		Qualident(
#line  604 "cs.ATG" 
out qualident);

#line  604 "cs.ATG" 
		ArrayList positional = new ArrayList();
		ArrayList named      = new ArrayList();
		string name = qualident;
		
		if (la.kind == 19) {
			AttributeArguments(
#line  608 "cs.ATG" 
ref positional, ref named);
		}

#line  608 "cs.ATG" 
		attribute  = new ICSharpCode.NRefactory.Parser.AST.Attribute(name, positional, named);
	}

	void AttributeArguments(
#line  611 "cs.ATG" 
ref ArrayList positional, ref ArrayList named) {

#line  613 "cs.ATG" 
		bool nameFound = false;
		string name = "";
		Expression expr;
		
		Expect(19);
		if (StartOf(4)) {
			if (
#line  621 "cs.ATG" 
IsAssignment()) {

#line  621 "cs.ATG" 
				nameFound = true; 
				lexer.NextToken();

#line  622 "cs.ATG" 
				name = t.val; 
				Expect(3);
			}
			Expr(
#line  624 "cs.ATG" 
out expr);

#line  624 "cs.ATG" 
			if (expr != null) {if(name == "") positional.Add(expr);
			else { named.Add(new NamedArgumentExpression(name, expr)); name = ""; }
			}
			
			while (la.kind == 13) {
				lexer.NextToken();
				if (
#line  632 "cs.ATG" 
IsAssignment()) {

#line  632 "cs.ATG" 
					nameFound = true; 
					Expect(1);

#line  633 "cs.ATG" 
					name = t.val; 
					Expect(3);
				} else if (StartOf(4)) {

#line  635 "cs.ATG" 
					if (nameFound) Error("no positional argument after named argument"); 
				} else SynErr(126);
				Expr(
#line  636 "cs.ATG" 
out expr);

#line  636 "cs.ATG" 
				if (expr != null) { if(name == "") positional.Add(expr);
				else { named.Add(new NamedArgumentExpression(name, expr)); name = ""; }
				}
				
			}
		}
		Expect(20);
	}

	void Expr(
#line  1849 "cs.ATG" 
out Expression expr) {

#line  1850 "cs.ATG" 
		expr = null; Expression expr1 = null, expr2 = null; 
		UnaryExpr(
#line  1852 "cs.ATG" 
out expr);
		if (StartOf(5)) {
			ConditionalOrExpr(
#line  1855 "cs.ATG" 
ref expr);
			if (la.kind == 12) {
				lexer.NextToken();
				Expr(
#line  1855 "cs.ATG" 
out expr1);
				Expect(9);
				Expr(
#line  1855 "cs.ATG" 
out expr2);

#line  1855 "cs.ATG" 
				expr = new ConditionalExpression(expr, expr1, expr2);  
			}
		} else if (StartOf(6)) {

#line  1857 "cs.ATG" 
			AssignmentOperatorType op; Expression val; 
			AssignmentOperator(
#line  1857 "cs.ATG" 
out op);
			Expr(
#line  1857 "cs.ATG" 
out val);

#line  1857 "cs.ATG" 
			expr = new AssignmentExpression(expr, op, val); 
		} else SynErr(127);
	}

	void AttributeSection(
#line  645 "cs.ATG" 
out AttributeSection section) {

#line  647 "cs.ATG" 
		string attributeTarget = "";
		ArrayList attributes = new ArrayList();
		ICSharpCode.NRefactory.Parser.AST.Attribute attribute;
		
		
		Expect(17);

#line  653 "cs.ATG" 
		Point startPos = t.Location; 
		if (
#line  654 "cs.ATG" 
IsLocalAttrTarget()) {
			if (la.kind == 67) {
				lexer.NextToken();

#line  655 "cs.ATG" 
				attributeTarget = "event";
			} else if (la.kind == 99) {
				lexer.NextToken();

#line  656 "cs.ATG" 
				attributeTarget = "return";
			} else {
				lexer.NextToken();

#line  657 "cs.ATG" 
				if (t.val != "field"    || t.val != "method" ||
				  t.val != "module"   || t.val != "param"  ||
				  t.val != "property" || t.val != "type")
				Error("attribute target specifier (event, return, field," +
				      "method, module, param, property, or type) expected");
				attributeTarget = t.val;
				
			}
			Expect(9);
		}
		Attribute(
#line  667 "cs.ATG" 
out attribute);

#line  667 "cs.ATG" 
		attributes.Add(attribute); 
		while (
#line  668 "cs.ATG" 
NotFinalComma()) {
			Expect(13);
			Attribute(
#line  668 "cs.ATG" 
out attribute);

#line  668 "cs.ATG" 
			attributes.Add(attribute); 
		}
		if (la.kind == 13) {
			lexer.NextToken();
		}
		Expect(18);

#line  670 "cs.ATG" 
		section = new AttributeSection(attributeTarget, attributes);
		section.StartLocation = startPos;
		section.EndLocation = t.EndLocation;
		
	}

	void TypeModifier(
#line  992 "cs.ATG" 
Modifiers m) {
		switch (la.kind) {
		case 87: {
			lexer.NextToken();

#line  994 "cs.ATG" 
			m.Add(Modifier.New); 
			break;
		}
		case 96: {
			lexer.NextToken();

#line  995 "cs.ATG" 
			m.Add(Modifier.Public); 
			break;
		}
		case 95: {
			lexer.NextToken();

#line  996 "cs.ATG" 
			m.Add(Modifier.Protected); 
			break;
		}
		case 82: {
			lexer.NextToken();

#line  997 "cs.ATG" 
			m.Add(Modifier.Internal); 
			break;
		}
		case 94: {
			lexer.NextToken();

#line  998 "cs.ATG" 
			m.Add(Modifier.Private); 
			break;
		}
		case 117: {
			lexer.NextToken();

#line  999 "cs.ATG" 
			m.Add(Modifier.Unsafe); 
			break;
		}
		case 47: {
			lexer.NextToken();

#line  1000 "cs.ATG" 
			m.Add(Modifier.Abstract); 
			break;
		}
		case 101: {
			lexer.NextToken();

#line  1001 "cs.ATG" 
			m.Add(Modifier.Sealed); 
			break;
		}
		case 105: {
			lexer.NextToken();

#line  1002 "cs.ATG" 
			m.Add(Modifier.Static); 
			break;
		}
		case 1: {
			lexer.NextToken();

#line  1003 "cs.ATG" 
			if (t.val == "partial") { m.Add(Modifier.Partial); } 
			break;
		}
		default: SynErr(128); break;
		}
	}

	void TypeDecl(
#line  703 "cs.ATG" 
Modifiers m, ArrayList attributes) {

#line  705 "cs.ATG" 
		TypeReference type;
		ArrayList names;
		ArrayList p; 
		string name;
		List<TemplateDefinition> templates;
		
		if (la.kind == 57) {

#line  711 "cs.ATG" 
			m.Check(Modifier.Classes); 
			lexer.NextToken();

#line  712 "cs.ATG" 
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			templates = newType.Templates;
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			
			newType.Type = Types.Class;
			
			Expect(1);

#line  719 "cs.ATG" 
			newType.Name = t.val; 
			if (la.kind == 22) {
				TypeParameterList(
#line  722 "cs.ATG" 
templates);
			}
			if (la.kind == 9) {
				ClassBase(
#line  724 "cs.ATG" 
out names);

#line  724 "cs.ATG" 
				newType.BaseTypes = names; 
			}

#line  724 "cs.ATG" 
			newType.StartLocation = t.EndLocation; 
			while (
#line  727 "cs.ATG" 
IdentIsWhere()) {
				TypeParameterConstraintsClause(
#line  727 "cs.ATG" 
templates);
			}
			ClassBody();
			if (la.kind == 11) {
				lexer.NextToken();
			}

#line  730 "cs.ATG" 
			newType.EndLocation = t.Location; 
			compilationUnit.BlockEnd();
			
		} else if (StartOf(7)) {

#line  733 "cs.ATG" 
			m.Check(Modifier.StructsInterfacesEnumsDelegates); 
			if (la.kind == 107) {
				lexer.NextToken();

#line  734 "cs.ATG" 
				TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
				templates = newType.Templates;
				compilationUnit.AddChild(newType);
				compilationUnit.BlockStart(newType);
				newType.Type = Types.Struct; 
				
				Expect(1);

#line  740 "cs.ATG" 
				newType.Name = t.val; 
				if (la.kind == 22) {
					TypeParameterList(
#line  743 "cs.ATG" 
templates);
				}
				if (la.kind == 9) {
					StructInterfaces(
#line  745 "cs.ATG" 
out names);

#line  745 "cs.ATG" 
					newType.BaseTypes = names; 
				}

#line  745 "cs.ATG" 
				newType.StartLocation = t.EndLocation; 
				while (
#line  748 "cs.ATG" 
IdentIsWhere()) {
					TypeParameterConstraintsClause(
#line  748 "cs.ATG" 
templates);
				}
				StructBody();
				if (la.kind == 11) {
					lexer.NextToken();
				}

#line  752 "cs.ATG" 
				newType.EndLocation = t.Location; 
				compilationUnit.BlockEnd();
				
			} else if (la.kind == 81) {
				lexer.NextToken();

#line  756 "cs.ATG" 
				TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
				templates = newType.Templates;
				compilationUnit.AddChild(newType);
				compilationUnit.BlockStart(newType);
				newType.Type = Types.Interface;
				
				Expect(1);

#line  762 "cs.ATG" 
				newType.Name = t.val; 
				if (la.kind == 22) {
					TypeParameterList(
#line  765 "cs.ATG" 
templates);
				}
				if (la.kind == 9) {
					InterfaceBase(
#line  767 "cs.ATG" 
out names);

#line  767 "cs.ATG" 
					newType.BaseTypes = names; 
				}

#line  767 "cs.ATG" 
				newType.StartLocation = t.EndLocation; 
				while (
#line  770 "cs.ATG" 
IdentIsWhere()) {
					TypeParameterConstraintsClause(
#line  770 "cs.ATG" 
templates);
				}
				InterfaceBody();
				if (la.kind == 11) {
					lexer.NextToken();
				}

#line  773 "cs.ATG" 
				newType.EndLocation = t.Location; 
				compilationUnit.BlockEnd();
				
			} else if (la.kind == 66) {
				lexer.NextToken();

#line  777 "cs.ATG" 
				TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
				compilationUnit.AddChild(newType);
				compilationUnit.BlockStart(newType);
				newType.Type = Types.Enum;
				
				Expect(1);

#line  782 "cs.ATG" 
				newType.Name = t.val; 
				if (la.kind == 9) {
					lexer.NextToken();
					IntegralType(
#line  783 "cs.ATG" 
out name);

#line  783 "cs.ATG" 
					newType.BaseTypes = new ArrayList(); 
					newType.BaseTypes.Add(name);
					
				}

#line  786 "cs.ATG" 
				newType.StartLocation = t.EndLocation; 
				EnumBody();
				if (la.kind == 11) {
					lexer.NextToken();
				}

#line  788 "cs.ATG" 
				newType.EndLocation = t.Location; 
				compilationUnit.BlockEnd();
				
			} else {
				lexer.NextToken();

#line  792 "cs.ATG" 
				DelegateDeclaration delegateDeclr = new DelegateDeclaration(m.Modifier, attributes);
				templates = delegateDeclr.Templates;
				delegateDeclr.StartLocation = t.Location;
				
				if (
#line  796 "cs.ATG" 
NotVoidPointer()) {
					Expect(121);

#line  796 "cs.ATG" 
					delegateDeclr.ReturnType = new TypeReference("void", 0, null); 
				} else if (StartOf(8)) {
					Type(
#line  797 "cs.ATG" 
out type);

#line  797 "cs.ATG" 
					delegateDeclr.ReturnType = type; 
				} else SynErr(129);
				Expect(1);

#line  799 "cs.ATG" 
				delegateDeclr.Name = t.val; 
				if (la.kind == 22) {
					TypeParameterList(
#line  802 "cs.ATG" 
templates);
				}
				Expect(19);
				if (StartOf(9)) {
					FormalParameterList(
#line  804 "cs.ATG" 
out p);

#line  804 "cs.ATG" 
					delegateDeclr.Parameters = p; 
				}
				Expect(20);
				while (
#line  808 "cs.ATG" 
IdentIsWhere()) {
					TypeParameterConstraintsClause(
#line  808 "cs.ATG" 
templates);
				}
				Expect(11);

#line  810 "cs.ATG" 
				delegateDeclr.EndLocation = t.Location;
				compilationUnit.AddChild(delegateDeclr);
				
			}
		} else SynErr(130);
	}

	void TypeParameterList(
#line  2203 "cs.ATG" 
List<TemplateDefinition> templates) {

#line  2205 "cs.ATG" 
		AttributeSection section;
		ArrayList attributes = new ArrayList();
		
		Expect(22);
		while (la.kind == 17) {
			AttributeSection(
#line  2209 "cs.ATG" 
out section);

#line  2209 "cs.ATG" 
			attributes.Add(section); 
		}
		Expect(1);

#line  2210 "cs.ATG" 
		templates.Add(new TemplateDefinition(t.val, attributes)); 
		while (la.kind == 13) {
			lexer.NextToken();
			while (la.kind == 17) {
				AttributeSection(
#line  2211 "cs.ATG" 
out section);

#line  2211 "cs.ATG" 
				attributes.Add(section); 
			}
			Expect(1);

#line  2212 "cs.ATG" 
			templates.Add(new TemplateDefinition(t.val, attributes)); 
		}
		Expect(21);
	}

	void ClassBase(
#line  825 "cs.ATG" 
out ArrayList names) {

#line  827 "cs.ATG" 
		string qualident;
		names = new ArrayList();
		List<TypeReference> types;
		
		Expect(9);
		ClassType(
#line  832 "cs.ATG" 
out qualident, out types);

#line  832 "cs.ATG" 
		names.Add(qualident); // TODO: enter the types 
		while (la.kind == 13) {
			lexer.NextToken();
			Qualident(
#line  833 "cs.ATG" 
out qualident);

#line  833 "cs.ATG" 
			names.Add(qualident); 
		}
	}

	void TypeParameterConstraintsClause(
#line  2216 "cs.ATG" 
List<TemplateDefinition> templates) {

#line  2217 "cs.ATG" 
		string name = ""; TypeReference type; 
		Expect(1);

#line  2219 "cs.ATG" 
		if (t.val != "where") Error("where expected"); 
		Expect(1);

#line  2220 "cs.ATG" 
		name = t.val; 
		Expect(9);
		TypeParameterConstraintsClauseBase(
#line  2222 "cs.ATG" 
out type);

#line  2223 "cs.ATG" 
		TemplateDefinition td = null;
		foreach (TemplateDefinition d in templates) {
			if (d.Name == name) {
				td = d;
				break;
			}
		}
		if ( td != null) { td.Bases.Add(type); }
		
		while (la.kind == 13) {
			lexer.NextToken();
			TypeParameterConstraintsClauseBase(
#line  2232 "cs.ATG" 
out type);

#line  2233 "cs.ATG" 
			td = null;
			foreach (TemplateDefinition d in templates) {
				if (d.Name == name) {
					td = d;
					break;
				}
			}
			if ( td != null) { td.Bases.Add(type); }
			
		}
	}

	void ClassBody() {

#line  837 "cs.ATG" 
		AttributeSection section; 
		Expect(15);
		while (StartOf(10)) {

#line  840 "cs.ATG" 
			ArrayList attributes = new ArrayList();
			Modifiers m = new Modifiers();
			
			while (la.kind == 17) {
				AttributeSection(
#line  843 "cs.ATG" 
out section);

#line  843 "cs.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(11)) {
				MemberModifier(
#line  844 "cs.ATG" 
m);
			}
			ClassMemberDecl(
#line  845 "cs.ATG" 
m, attributes);
		}
		Expect(16);
	}

	void StructInterfaces(
#line  850 "cs.ATG" 
out ArrayList names) {

#line  852 "cs.ATG" 
		string qualident; 
		names = new ArrayList();
		
		Expect(9);
		Qualident(
#line  856 "cs.ATG" 
out qualident);

#line  856 "cs.ATG" 
		names.Add(qualident); 
		while (la.kind == 13) {
			lexer.NextToken();
			Qualident(
#line  857 "cs.ATG" 
out qualident);

#line  857 "cs.ATG" 
			names.Add(qualident); 
		}
	}

	void StructBody() {

#line  861 "cs.ATG" 
		AttributeSection section; 
		Expect(15);
		while (StartOf(12)) {

#line  864 "cs.ATG" 
			ArrayList attributes = new ArrayList();
			Modifiers m = new Modifiers();
			
			while (la.kind == 17) {
				AttributeSection(
#line  867 "cs.ATG" 
out section);

#line  867 "cs.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(11)) {
				MemberModifier(
#line  868 "cs.ATG" 
m);
			}
			StructMemberDecl(
#line  869 "cs.ATG" 
m, attributes);
		}
		Expect(16);
	}

	void InterfaceBase(
#line  874 "cs.ATG" 
out ArrayList names) {

#line  876 "cs.ATG" 
		string qualident;
		names = new ArrayList();
		
		Expect(9);
		Qualident(
#line  880 "cs.ATG" 
out qualident);

#line  880 "cs.ATG" 
		names.Add(qualident); 
		while (la.kind == 13) {
			lexer.NextToken();
			Qualident(
#line  881 "cs.ATG" 
out qualident);

#line  881 "cs.ATG" 
			names.Add(qualident); 
		}
	}

	void InterfaceBody() {
		Expect(15);
		while (StartOf(13)) {
			InterfaceMemberDecl();
		}
		Expect(16);
	}

	void IntegralType(
#line  1017 "cs.ATG" 
out string name) {

#line  1017 "cs.ATG" 
		name = ""; 
		switch (la.kind) {
		case 100: {
			lexer.NextToken();

#line  1019 "cs.ATG" 
			name = "sbyte"; 
			break;
		}
		case 52: {
			lexer.NextToken();

#line  1020 "cs.ATG" 
			name = "byte"; 
			break;
		}
		case 102: {
			lexer.NextToken();

#line  1021 "cs.ATG" 
			name = "short"; 
			break;
		}
		case 118: {
			lexer.NextToken();

#line  1022 "cs.ATG" 
			name = "ushort"; 
			break;
		}
		case 80: {
			lexer.NextToken();

#line  1023 "cs.ATG" 
			name = "int"; 
			break;
		}
		case 114: {
			lexer.NextToken();

#line  1024 "cs.ATG" 
			name = "uint"; 
			break;
		}
		case 85: {
			lexer.NextToken();

#line  1025 "cs.ATG" 
			name = "long"; 
			break;
		}
		case 115: {
			lexer.NextToken();

#line  1026 "cs.ATG" 
			name = "ulong"; 
			break;
		}
		case 55: {
			lexer.NextToken();

#line  1027 "cs.ATG" 
			name = "char"; 
			break;
		}
		default: SynErr(131); break;
		}
	}

	void EnumBody() {

#line  887 "cs.ATG" 
		FieldDeclaration f; 
		Expect(15);
		if (la.kind == 1 || la.kind == 17) {
			EnumMemberDecl(
#line  889 "cs.ATG" 
out f);

#line  889 "cs.ATG" 
			compilationUnit.AddChild(f); 
			while (
#line  890 "cs.ATG" 
NotFinalComma()) {
				Expect(13);
				EnumMemberDecl(
#line  890 "cs.ATG" 
out f);

#line  890 "cs.ATG" 
				compilationUnit.AddChild(f); 
			}
			if (la.kind == 13) {
				lexer.NextToken();
			}
		}
		Expect(16);
	}

	void Type(
#line  895 "cs.ATG" 
out TypeReference type) {

#line  897 "cs.ATG" 
		string name = "";
		int pointer = 0;
		List<TypeReference> types = null;
		type = null;
		
		if (la.kind == 1 || la.kind == 89 || la.kind == 106) {
			ClassType(
#line  903 "cs.ATG" 
out name, out types);
		} else if (StartOf(14)) {
			SimpleType(
#line  904 "cs.ATG" 
out name);
		} else if (la.kind == 121) {
			lexer.NextToken();
			Expect(6);

#line  905 "cs.ATG" 
			pointer = 1; name = "void"; 
		} else SynErr(132);

#line  906 "cs.ATG" 
		ArrayList r = new ArrayList(); 
		while (
#line  908 "cs.ATG" 
IsPointerOrDims()) {

#line  908 "cs.ATG" 
			int i = 0; 
			if (la.kind == 6) {
				lexer.NextToken();

#line  909 "cs.ATG" 
				++pointer; 
			} else if (la.kind == 17) {
				lexer.NextToken();
				while (la.kind == 13) {
					lexer.NextToken();

#line  910 "cs.ATG" 
					++i; 
				}
				Expect(18);

#line  910 "cs.ATG" 
				r.Add(i); 
			} else SynErr(133);
		}

#line  912 "cs.ATG" 
		int[] rank = new int[r.Count]; r.CopyTo(rank); 
		type = new TypeReference(name, pointer, rank, types);
		
	}

	void FormalParameterList(
#line  947 "cs.ATG" 
out ArrayList parameter) {

#line  949 "cs.ATG" 
		parameter = new ArrayList();
		ParameterDeclarationExpression p;
		AttributeSection section;
		ArrayList attributes = new ArrayList();
		
		while (la.kind == 17) {
			AttributeSection(
#line  955 "cs.ATG" 
out section);

#line  955 "cs.ATG" 
			attributes.Add(section); 
		}
		if (StartOf(15)) {
			FixedParameter(
#line  957 "cs.ATG" 
out p);

#line  957 "cs.ATG" 
			bool paramsFound = false;
			p.Attributes = attributes;
			parameter.Add(p);
			
			while (la.kind == 13) {
				lexer.NextToken();

#line  962 "cs.ATG" 
				attributes = new ArrayList(); if (paramsFound) Error("params array must be at end of parameter list"); 
				while (la.kind == 17) {
					AttributeSection(
#line  963 "cs.ATG" 
out section);

#line  963 "cs.ATG" 
					attributes.Add(section); 
				}
				if (StartOf(15)) {
					FixedParameter(
#line  965 "cs.ATG" 
out p);

#line  965 "cs.ATG" 
					p.Attributes = attributes; parameter.Add(p); 
				} else if (la.kind == 93) {
					ParameterArray(
#line  966 "cs.ATG" 
out p);

#line  966 "cs.ATG" 
					paramsFound = true; p.Attributes = attributes; parameter.Add(p); 
				} else SynErr(134);
			}
		} else if (la.kind == 93) {
			ParameterArray(
#line  969 "cs.ATG" 
out p);

#line  969 "cs.ATG" 
			p.Attributes = attributes; parameter.Add(p); 
		} else SynErr(135);
	}

	void ClassType(
#line  1006 "cs.ATG" 
out string name, out List<TypeReference> types) {

#line  1007 "cs.ATG" 
		string qualident; name = "";
		List<TypeReference> t;
		types = null;
		
		if (la.kind == 1) {
			TypeName(
#line  1012 "cs.ATG" 
out qualident, out t);

#line  1012 "cs.ATG" 
			name = qualident; types = t; 
		} else if (la.kind == 89) {
			lexer.NextToken();

#line  1013 "cs.ATG" 
			name = "object"; 
		} else if (la.kind == 106) {
			lexer.NextToken();

#line  1014 "cs.ATG" 
			name = "string"; 
		} else SynErr(136);
	}

	void MemberModifier(
#line  1030 "cs.ATG" 
Modifiers m) {
		switch (la.kind) {
		case 47: {
			lexer.NextToken();

#line  1032 "cs.ATG" 
			m.Add(Modifier.Abstract); 
			break;
		}
		case 69: {
			lexer.NextToken();

#line  1033 "cs.ATG" 
			m.Add(Modifier.Extern); 
			break;
		}
		case 82: {
			lexer.NextToken();

#line  1034 "cs.ATG" 
			m.Add(Modifier.Internal); 
			break;
		}
		case 87: {
			lexer.NextToken();

#line  1035 "cs.ATG" 
			m.Add(Modifier.New); 
			break;
		}
		case 92: {
			lexer.NextToken();

#line  1036 "cs.ATG" 
			m.Add(Modifier.Override); 
			break;
		}
		case 94: {
			lexer.NextToken();

#line  1037 "cs.ATG" 
			m.Add(Modifier.Private); 
			break;
		}
		case 95: {
			lexer.NextToken();

#line  1038 "cs.ATG" 
			m.Add(Modifier.Protected); 
			break;
		}
		case 96: {
			lexer.NextToken();

#line  1039 "cs.ATG" 
			m.Add(Modifier.Public); 
			break;
		}
		case 97: {
			lexer.NextToken();

#line  1040 "cs.ATG" 
			m.Add(Modifier.Readonly); 
			break;
		}
		case 101: {
			lexer.NextToken();

#line  1041 "cs.ATG" 
			m.Add(Modifier.Sealed); 
			break;
		}
		case 105: {
			lexer.NextToken();

#line  1042 "cs.ATG" 
			m.Add(Modifier.Static); 
			break;
		}
		case 117: {
			lexer.NextToken();

#line  1043 "cs.ATG" 
			m.Add(Modifier.Unsafe); 
			break;
		}
		case 120: {
			lexer.NextToken();

#line  1044 "cs.ATG" 
			m.Add(Modifier.Virtual); 
			break;
		}
		case 122: {
			lexer.NextToken();

#line  1045 "cs.ATG" 
			m.Add(Modifier.Volatile); 
			break;
		}
		default: SynErr(137); break;
		}
	}

	void ClassMemberDecl(
#line  1278 "cs.ATG" 
Modifiers m, ArrayList attributes) {

#line  1279 "cs.ATG" 
		Statement stmt = null; 
		if (StartOf(16)) {
			StructMemberDecl(
#line  1281 "cs.ATG" 
m, attributes);
		} else if (la.kind == 26) {

#line  1282 "cs.ATG" 
			m.Check(Modifier.Destructors); Point startPos = t.Location; 
			lexer.NextToken();
			Expect(1);

#line  1283 "cs.ATG" 
			DestructorDeclaration d = new DestructorDeclaration(t.val, m.Modifier, attributes); 
			d.Modifier = m.Modifier;
			d.StartLocation = startPos;
			
			Expect(19);
			Expect(20);

#line  1287 "cs.ATG" 
			d.EndLocation = t.EndLocation; 
			if (la.kind == 15) {
				Block(
#line  1287 "cs.ATG" 
out stmt);
			} else if (la.kind == 11) {
				lexer.NextToken();
			} else SynErr(138);

#line  1288 "cs.ATG" 
			d.Body = (BlockStatement)stmt;
			compilationUnit.AddChild(d);
			
		} else SynErr(139);
	}

	void StructMemberDecl(
#line  1048 "cs.ATG" 
Modifiers m, ArrayList attributes) {

#line  1050 "cs.ATG" 
		string qualident = null;
		TypeReference type;
		Expression expr;
		ArrayList p = new ArrayList();
		Statement stmt = null;
		ArrayList variableDeclarators = new ArrayList();
		List<TemplateDefinition> templates = new List<TemplateDefinition>();
		
		if (la.kind == 58) {

#line  1059 "cs.ATG" 
			m.Check(Modifier.Constants); 
			lexer.NextToken();

#line  1060 "cs.ATG" 
			Point startPos = t.Location; 
			Type(
#line  1061 "cs.ATG" 
out type);
			Expect(1);

#line  1061 "cs.ATG" 
			FieldDeclaration fd = new FieldDeclaration(attributes, type, m.Modifier | Modifier.Const);
			fd.StartLocation = startPos;
			VariableDeclaration f = new VariableDeclaration(t.val);
			fd.Fields.Add(f);
			
			Expect(3);
			Expr(
#line  1066 "cs.ATG" 
out expr);

#line  1066 "cs.ATG" 
			f.Initializer = expr; 
			while (la.kind == 13) {
				lexer.NextToken();
				Expect(1);

#line  1067 "cs.ATG" 
				f = new VariableDeclaration(t.val);
				fd.Fields.Add(f);
				
				Expect(3);
				Expr(
#line  1070 "cs.ATG" 
out expr);

#line  1070 "cs.ATG" 
				f.Initializer = expr; 
			}
			Expect(11);

#line  1071 "cs.ATG" 
			fd.EndLocation = t.EndLocation; compilationUnit.AddChild(fd); 
		} else if (
#line  1074 "cs.ATG" 
NotVoidPointer()) {

#line  1074 "cs.ATG" 
			m.Check(Modifier.PropertysEventsMethods); 
			Expect(121);

#line  1075 "cs.ATG" 
			Point startPos = t.Location; 
			Qualident(
#line  1076 "cs.ATG" 
out qualident);
			if (la.kind == 22) {
				TypeParameterList(
#line  1078 "cs.ATG" 
templates);
			}
			Expect(19);
			if (StartOf(9)) {
				FormalParameterList(
#line  1081 "cs.ATG" 
out p);
			}
			Expect(20);

#line  1081 "cs.ATG" 
			MethodDeclaration methodDeclaration = new MethodDeclaration(qualident, 
			                                                           m.Modifier, 
			                                                           new TypeReference("void"), 
			                                                           p, 
			                                                           attributes);
			methodDeclaration.StartLocation = startPos;
			methodDeclaration.EndLocation   = t.EndLocation;
			methodDeclaration.Templates = templates;
			compilationUnit.AddChild(methodDeclaration);
			compilationUnit.BlockStart(methodDeclaration);
			
			while (
#line  1094 "cs.ATG" 
IdentIsWhere()) {
				TypeParameterConstraintsClause(
#line  1094 "cs.ATG" 
templates);
			}
			if (la.kind == 15) {
				Block(
#line  1096 "cs.ATG" 
out stmt);
			} else if (la.kind == 11) {
				lexer.NextToken();
			} else SynErr(140);

#line  1096 "cs.ATG" 
			compilationUnit.BlockEnd();
			methodDeclaration.Body  = (BlockStatement)stmt;
			
		} else if (la.kind == 67) {

#line  1100 "cs.ATG" 
			m.Check(Modifier.PropertysEventsMethods); 
			lexer.NextToken();

#line  1101 "cs.ATG" 
			EventDeclaration eventDecl = new EventDeclaration(m.Modifier, attributes);
			eventDecl.StartLocation = t.Location;
			compilationUnit.AddChild(eventDecl);
			compilationUnit.BlockStart(eventDecl);
			EventAddRegion addBlock = null;
			EventRemoveRegion removeBlock = null;
			
			Type(
#line  1108 "cs.ATG" 
out type);

#line  1108 "cs.ATG" 
			eventDecl.TypeReference = type; 
			if (
#line  1110 "cs.ATG" 
IsVarDecl()) {
				VariableDeclarator(
#line  1110 "cs.ATG" 
variableDeclarators);
				while (la.kind == 13) {
					lexer.NextToken();
					VariableDeclarator(
#line  1111 "cs.ATG" 
variableDeclarators);
				}
				Expect(11);

#line  1111 "cs.ATG" 
				eventDecl.VariableDeclarators = variableDeclarators; eventDecl.EndLocation = t.EndLocation;  
			} else if (la.kind == 1) {
				Qualident(
#line  1112 "cs.ATG" 
out qualident);

#line  1112 "cs.ATG" 
				eventDecl.Name = qualident; eventDecl.EndLocation = t.EndLocation;  
				Expect(15);

#line  1113 "cs.ATG" 
				eventDecl.BodyStart = t.Location; 
				EventAccessorDecls(
#line  1114 "cs.ATG" 
out addBlock, out removeBlock);
				Expect(16);

#line  1115 "cs.ATG" 
				eventDecl.BodyEnd   = t.EndLocation; 
			} else SynErr(141);

#line  1116 "cs.ATG" 
			compilationUnit.BlockEnd();
			
			eventDecl.AddRegion = addBlock;
			eventDecl.RemoveRegion = removeBlock;
			
		} else if (
#line  1123 "cs.ATG" 
IdentAndLPar()) {

#line  1123 "cs.ATG" 
			m.Check(Modifier.Constructors | Modifier.StaticConstructors); 
			Expect(1);

#line  1124 "cs.ATG" 
			string name = t.val; Point startPos = t.Location; 
			Expect(19);
			if (StartOf(9)) {

#line  1124 "cs.ATG" 
				m.Check(Modifier.Constructors); 
				FormalParameterList(
#line  1125 "cs.ATG" 
out p);
			}
			Expect(20);

#line  1127 "cs.ATG" 
			ConstructorInitializer init = null;  
			if (la.kind == 9) {

#line  1128 "cs.ATG" 
				m.Check(Modifier.Constructors); 
				ConstructorInitializer(
#line  1129 "cs.ATG" 
out init);
			}

#line  1131 "cs.ATG" 
			ConstructorDeclaration cd = new ConstructorDeclaration(name, m.Modifier, p, init, attributes); 
			cd.StartLocation = startPos;
			cd.EndLocation   = t.EndLocation;
			
			if (la.kind == 15) {
				Block(
#line  1136 "cs.ATG" 
out stmt);
			} else if (la.kind == 11) {
				lexer.NextToken();
			} else SynErr(142);

#line  1136 "cs.ATG" 
			cd.Body = (BlockStatement)stmt; compilationUnit.AddChild(cd); 
		} else if (la.kind == 68 || la.kind == 78) {

#line  1139 "cs.ATG" 
			m.Check(Modifier.Operators);
			if (m.isNone) Error("at least one modifier must be set"); 
			bool isImplicit = true;
			
			if (la.kind == 78) {
				lexer.NextToken();
			} else {
				lexer.NextToken();

#line  1143 "cs.ATG" 
				isImplicit = false; 
			}
			Expect(90);
			Type(
#line  1144 "cs.ATG" 
out type);

#line  1144 "cs.ATG" 
			TypeReference operatorType = type; 
			Expect(19);
			Type(
#line  1145 "cs.ATG" 
out type);
			Expect(1);

#line  1145 "cs.ATG" 
			string varName = t.val; 
			Expect(20);
			if (la.kind == 15) {
				Block(
#line  1145 "cs.ATG" 
out stmt);
			} else if (la.kind == 11) {
				lexer.NextToken();

#line  1145 "cs.ATG" 
				stmt = null; 
			} else SynErr(143);

#line  1148 "cs.ATG" 
			ArrayList parameters = new ArrayList();
			parameters.Add(new ParameterDeclarationExpression(type, varName));
			OperatorDeclaration operatorDeclaration = new OperatorDeclaration(m.Modifier, 
			                                                                  attributes, 
			                                                                  parameters, 
			                                                                  operatorType,
			                                                                  isImplicit ? ConversionType.Implicit : ConversionType.Explicit
			                                                                  );
			operatorDeclaration.Body = (BlockStatement)stmt;
			compilationUnit.AddChild(operatorDeclaration);
			
		} else if (StartOf(17)) {
			TypeDecl(
#line  1161 "cs.ATG" 
m, attributes);
		} else if (StartOf(8)) {
			Type(
#line  1162 "cs.ATG" 
out type);

#line  1162 "cs.ATG" 
			Point startPos = t.Location;  
			if (la.kind == 90) {

#line  1164 "cs.ATG" 
				OverloadableOperatorType op;
				m.Check(Modifier.Operators);
				if (m.isNone) Error("at least one modifier must be set");
				
				lexer.NextToken();
				OverloadableOperator(
#line  1168 "cs.ATG" 
out op);

#line  1168 "cs.ATG" 
				TypeReference firstType, secondType = null; string secondName = null; 
				Expect(19);
				Type(
#line  1169 "cs.ATG" 
out firstType);
				Expect(1);

#line  1169 "cs.ATG" 
				string firstName = t.val; 
				if (la.kind == 13) {
					lexer.NextToken();
					Type(
#line  1170 "cs.ATG" 
out secondType);
					Expect(1);

#line  1170 "cs.ATG" 
					secondName = t.val; 
				} else if (la.kind == 20) {
				} else SynErr(144);
				Expect(20);
				if (la.kind == 15) {
					Block(
#line  1178 "cs.ATG" 
out stmt);
				} else if (la.kind == 11) {
					lexer.NextToken();
				} else SynErr(145);

#line  1180 "cs.ATG" 
				ArrayList parameters = new ArrayList();
				parameters.Add(new ParameterDeclarationExpression(firstType, firstName));
				if (secondType != null) {
					parameters.Add(new ParameterDeclarationExpression(secondType, secondName));
				}
				OperatorDeclaration operatorDeclaration = new OperatorDeclaration(m.Modifier,
				                                                                  attributes,
				                                                                  parameters,
				                                                                  type,
				                                                                  op);
				operatorDeclaration.Body = (BlockStatement)stmt;
				compilationUnit.AddChild(operatorDeclaration);
				
			} else if (
#line  1195 "cs.ATG" 
IsVarDecl()) {

#line  1195 "cs.ATG" 
				m.Check(Modifier.Fields); 
				FieldDeclaration fd = new FieldDeclaration(attributes, type, m.Modifier);
				fd.StartLocation = startPos; 
				
				VariableDeclarator(
#line  1199 "cs.ATG" 
variableDeclarators);
				while (la.kind == 13) {
					lexer.NextToken();
					VariableDeclarator(
#line  1200 "cs.ATG" 
variableDeclarators);
				}
				Expect(11);

#line  1201 "cs.ATG" 
				fd.EndLocation = t.EndLocation; fd.Fields = variableDeclarators; compilationUnit.AddChild(fd); 
			} else if (la.kind == 109) {

#line  1204 "cs.ATG" 
				m.Check(Modifier.Indexers); 
				lexer.NextToken();
				Expect(17);
				FormalParameterList(
#line  1205 "cs.ATG" 
out p);
				Expect(18);

#line  1205 "cs.ATG" 
				Point endLocation = t.EndLocation; 
				Expect(15);

#line  1206 "cs.ATG" 
				IndexerDeclaration indexer = new IndexerDeclaration(type, p, m.Modifier, attributes);
				indexer.StartLocation = startPos;
				indexer.EndLocation   = endLocation;
				indexer.BodyStart     = t.Location;
				PropertyGetRegion getRegion;
				PropertySetRegion setRegion;
				
				AccessorDecls(
#line  1213 "cs.ATG" 
out getRegion, out setRegion);
				Expect(16);

#line  1214 "cs.ATG" 
				indexer.BodyEnd    = t.EndLocation;
				indexer.GetRegion = getRegion;
				indexer.SetRegion = setRegion;
				compilationUnit.AddChild(indexer);
				
			} else if (la.kind == 1) {
				Qualident(
#line  1219 "cs.ATG" 
out qualident);

#line  1219 "cs.ATG" 
				Point qualIdentEndLocation = t.EndLocation; 
				if (la.kind == 15 || la.kind == 19 || la.kind == 22) {
					if (la.kind == 19 || la.kind == 22) {

#line  1222 "cs.ATG" 
						m.Check(Modifier.PropertysEventsMethods); 
						if (la.kind == 22) {
							TypeParameterList(
#line  1224 "cs.ATG" 
templates);
						}
						Expect(19);
						if (StartOf(9)) {
							FormalParameterList(
#line  1225 "cs.ATG" 
out p);
						}
						Expect(20);

#line  1226 "cs.ATG" 
						MethodDeclaration methodDeclaration = new MethodDeclaration(qualident,
						                                                           m.Modifier, 
						                                                           type, 
						                                                           p, 
						                                                           attributes);
						methodDeclaration.StartLocation = startPos;
						methodDeclaration.EndLocation   = t.EndLocation;
						methodDeclaration.Templates = templates;
						compilationUnit.AddChild(methodDeclaration);
						                                      
						while (
#line  1236 "cs.ATG" 
IdentIsWhere()) {
							TypeParameterConstraintsClause(
#line  1236 "cs.ATG" 
templates);
						}
						if (la.kind == 15) {
							Block(
#line  1237 "cs.ATG" 
out stmt);
						} else if (la.kind == 11) {
							lexer.NextToken();
						} else SynErr(146);

#line  1237 "cs.ATG" 
						methodDeclaration.Body  = (BlockStatement)stmt; 
					} else {
						lexer.NextToken();

#line  1240 "cs.ATG" 
						PropertyDeclaration pDecl = new PropertyDeclaration(qualident, type, m.Modifier, attributes); 
						pDecl.StartLocation = startPos;
						pDecl.EndLocation   = qualIdentEndLocation;
						pDecl.BodyStart   = t.Location;
						PropertyGetRegion getRegion;
						PropertySetRegion setRegion;
						
						AccessorDecls(
#line  1247 "cs.ATG" 
out getRegion, out setRegion);
						Expect(16);

#line  1249 "cs.ATG" 
						pDecl.GetRegion = getRegion;
						pDecl.SetRegion = setRegion;
						pDecl.BodyEnd = t.EndLocation;
						compilationUnit.AddChild(pDecl);
						
					}
				} else if (la.kind == 14) {

#line  1257 "cs.ATG" 
					m.Check(Modifier.Indexers); 
					lexer.NextToken();
					Expect(109);
					Expect(17);
					FormalParameterList(
#line  1258 "cs.ATG" 
out p);
					Expect(18);

#line  1259 "cs.ATG" 
					IndexerDeclaration indexer = new IndexerDeclaration(type, p, m.Modifier, attributes);
					indexer.StartLocation = startPos;
					indexer.EndLocation   = t.EndLocation;
					indexer.NamespaceName = qualident;
					PropertyGetRegion getRegion;
					PropertySetRegion setRegion;
					
					Expect(15);

#line  1266 "cs.ATG" 
					Point bodyStart = t.Location; 
					AccessorDecls(
#line  1267 "cs.ATG" 
out getRegion, out setRegion);
					Expect(16);

#line  1268 "cs.ATG" 
					indexer.BodyStart = bodyStart;
					indexer.BodyEnd   = t.EndLocation;
					indexer.GetRegion = getRegion;
					indexer.SetRegion = setRegion;
					compilationUnit.AddChild(indexer);
					
				} else SynErr(147);
			} else SynErr(148);
		} else SynErr(149);
	}

	void InterfaceMemberDecl() {

#line  1295 "cs.ATG" 
		TypeReference type;
		ArrayList p;
		AttributeSection section;
		Modifier mod = Modifier.None;
		ArrayList attributes = new ArrayList();
		ArrayList parameters = new ArrayList();
		string name;
		PropertyGetRegion getBlock;
		PropertySetRegion setBlock;
		Point startLocation = new Point(-1, -1);
		List<TemplateDefinition> templates = new List<TemplateDefinition>();
		
		while (la.kind == 17) {
			AttributeSection(
#line  1308 "cs.ATG" 
out section);

#line  1308 "cs.ATG" 
			attributes.Add(section); 
		}
		if (la.kind == 87) {
			lexer.NextToken();

#line  1309 "cs.ATG" 
			mod = Modifier.New; startLocation = t.Location; 
		}
		if (
#line  1312 "cs.ATG" 
NotVoidPointer()) {
			Expect(121);

#line  1312 "cs.ATG" 
			if (startLocation.X == -1) startLocation = t.Location; 
			Expect(1);

#line  1312 "cs.ATG" 
			name = t.val; 
			Expect(19);
			if (StartOf(9)) {
				FormalParameterList(
#line  1313 "cs.ATG" 
out parameters);
			}
			Expect(20);
			Expect(11);

#line  1313 "cs.ATG" 
			MethodDeclaration md = new MethodDeclaration(name, mod, new TypeReference("void"), parameters, attributes);
			md.StartLocation = startLocation;
			md.EndLocation = t.EndLocation;
			compilationUnit.AddChild(md);
			
		} else if (StartOf(18)) {
			if (StartOf(8)) {
				Type(
#line  1319 "cs.ATG" 
out type);

#line  1319 "cs.ATG" 
				if (startLocation.X == -1) startLocation = t.Location; 
				if (la.kind == 1) {
					lexer.NextToken();

#line  1321 "cs.ATG" 
					name = t.val; Point qualIdentEndLocation = t.EndLocation; 
					if (la.kind == 19 || la.kind == 22) {
						if (la.kind == 22) {
							TypeParameterList(
#line  1325 "cs.ATG" 
templates);
						}
						Expect(19);
						if (StartOf(9)) {
							FormalParameterList(
#line  1326 "cs.ATG" 
out parameters);
						}
						Expect(20);
						while (
#line  1328 "cs.ATG" 
IdentIsWhere()) {
							TypeParameterConstraintsClause(
#line  1328 "cs.ATG" 
templates);
						}
						Expect(11);

#line  1329 "cs.ATG" 
						MethodDeclaration md = new MethodDeclaration(name, mod, type, parameters, attributes);
						                                      md.StartLocation = startLocation;
						                                      md.EndLocation = t.EndLocation;
						                                      compilationUnit.AddChild(md);
						                                   
					} else if (la.kind == 15) {

#line  1335 "cs.ATG" 
						PropertyDeclaration pd = new PropertyDeclaration(name, type, mod, attributes); compilationUnit.AddChild(pd); 
						lexer.NextToken();

#line  1336 "cs.ATG" 
						Point bodyStart = t.Location;
						InterfaceAccessors(
#line  1336 "cs.ATG" 
out getBlock, out setBlock);
						Expect(16);

#line  1336 "cs.ATG" 
						pd.GetRegion = getBlock; pd.SetRegion = setBlock; pd.StartLocation = startLocation; pd.EndLocation = qualIdentEndLocation; pd.BodyStart = bodyStart; pd.BodyEnd = t.EndLocation; 
					} else SynErr(150);
				} else if (la.kind == 109) {
					lexer.NextToken();
					Expect(17);
					FormalParameterList(
#line  1339 "cs.ATG" 
out p);
					Expect(18);

#line  1339 "cs.ATG" 
					Point bracketEndLocation = t.EndLocation; 

#line  1339 "cs.ATG" 
					IndexerDeclaration id = new IndexerDeclaration(type, p, mod, attributes); compilationUnit.AddChild(id); 
					Expect(15);

#line  1340 "cs.ATG" 
					Point bodyStart = t.Location;
					InterfaceAccessors(
#line  1340 "cs.ATG" 
out getBlock, out setBlock);
					Expect(16);

#line  1340 "cs.ATG" 
					id.GetRegion = getBlock; id.SetRegion = setBlock; id.StartLocation = startLocation;  id.EndLocation = bracketEndLocation; id.BodyStart = bodyStart; id.BodyEnd = t.EndLocation;
				} else SynErr(151);
			} else {
				lexer.NextToken();

#line  1343 "cs.ATG" 
				if (startLocation.X == -1) startLocation = t.Location; 
				Type(
#line  1343 "cs.ATG" 
out type);
				Expect(1);

#line  1343 "cs.ATG" 
				EventDeclaration ed = new EventDeclaration(type, t.val, mod, attributes);
				compilationUnit.AddChild(ed);
				
				Expect(11);

#line  1346 "cs.ATG" 
				ed.StartLocation = startLocation; ed.EndLocation = t.EndLocation; 
			}
		} else SynErr(152);
	}

	void EnumMemberDecl(
#line  1351 "cs.ATG" 
out FieldDeclaration f) {

#line  1353 "cs.ATG" 
		Expression expr = null;
		ArrayList attributes = new ArrayList();
		AttributeSection section = null;
		VariableDeclaration varDecl = null;
		
		while (la.kind == 17) {
			AttributeSection(
#line  1359 "cs.ATG" 
out section);

#line  1359 "cs.ATG" 
			attributes.Add(section); 
		}
		Expect(1);

#line  1360 "cs.ATG" 
		f = new FieldDeclaration(attributes);
		varDecl         = new VariableDeclaration(t.val);
		f.Fields.Add(varDecl);
		f.StartLocation = t.Location;
		
		if (la.kind == 3) {
			lexer.NextToken();
			Expr(
#line  1365 "cs.ATG" 
out expr);

#line  1365 "cs.ATG" 
			varDecl.Initializer = expr; 
		}
	}

	void SimpleType(
#line  936 "cs.ATG" 
out string name) {

#line  937 "cs.ATG" 
		name = String.Empty; 
		if (StartOf(19)) {
			IntegralType(
#line  939 "cs.ATG" 
out name);
		} else if (la.kind == 73) {
			lexer.NextToken();

#line  940 "cs.ATG" 
			name = "float"; 
		} else if (la.kind == 64) {
			lexer.NextToken();

#line  941 "cs.ATG" 
			name = "double"; 
		} else if (la.kind == 60) {
			lexer.NextToken();

#line  942 "cs.ATG" 
			name = "decimal"; 
		} else if (la.kind == 50) {
			lexer.NextToken();

#line  943 "cs.ATG" 
			name = "bool"; 
		} else SynErr(153);
	}

	void NonArrayType(
#line  917 "cs.ATG" 
out TypeReference type) {

#line  919 "cs.ATG" 
		string name = "";
		int pointer = 0;
		List<TypeReference> genericTypes = null;
		
		if (la.kind == 1 || la.kind == 89 || la.kind == 106) {
			ClassType(
#line  924 "cs.ATG" 
out name, out genericTypes);
		} else if (StartOf(14)) {
			SimpleType(
#line  925 "cs.ATG" 
out name);
		} else if (la.kind == 121) {
			lexer.NextToken();
			Expect(6);

#line  926 "cs.ATG" 
			pointer = 1; name = "void"; 
		} else SynErr(154);
		while (
#line  929 "cs.ATG" 
IsPointer()) {
			Expect(6);

#line  930 "cs.ATG" 
			++pointer; 
		}

#line  932 "cs.ATG" 
		type = new TypeReference(name, pointer, null, genericTypes);
		
	}

	void FixedParameter(
#line  973 "cs.ATG" 
out ParameterDeclarationExpression p) {

#line  975 "cs.ATG" 
		TypeReference type;
		ParamModifier mod = ParamModifier.In;
		
		if (la.kind == 91 || la.kind == 98) {
			if (la.kind == 98) {
				lexer.NextToken();

#line  980 "cs.ATG" 
				mod = ParamModifier.Ref; 
			} else {
				lexer.NextToken();

#line  981 "cs.ATG" 
				mod = ParamModifier.Out; 
			}
		}
		Type(
#line  983 "cs.ATG" 
out type);
		Expect(1);

#line  983 "cs.ATG" 
		p = new ParameterDeclarationExpression(type, t.val, mod); 
	}

	void ParameterArray(
#line  986 "cs.ATG" 
out ParameterDeclarationExpression p) {

#line  987 "cs.ATG" 
		TypeReference type; 
		Expect(93);
		Type(
#line  989 "cs.ATG" 
out type);
		Expect(1);

#line  989 "cs.ATG" 
		p = new ParameterDeclarationExpression(type, t.val, ParamModifier.Params); 
	}

	void TypeName(
#line  2185 "cs.ATG" 
out string qualident, out List<TypeReference> types) {

#line  2186 "cs.ATG" 
		List<TypeReference> t; types = new List<TypeReference>(); 
		Qualident(
#line  2188 "cs.ATG" 
out qualident);
		if (la.kind == 22) {
			TypeArgumentList(
#line  2189 "cs.ATG" 
out t);

#line  2189 "cs.ATG" 
			types = t; 
		}
	}

	void Block(
#line  1469 "cs.ATG" 
out Statement stmt) {
		Expect(15);

#line  1471 "cs.ATG" 
		BlockStatement blockStmt = new BlockStatement();
		blockStmt.StartLocation = t.EndLocation;
		compilationUnit.BlockStart(blockStmt);
		if (!parseMethodContents) lexer.SkipCurrentBlock();
		
		while (StartOf(20)) {
			Statement();
		}
		Expect(16);

#line  1478 "cs.ATG" 
		stmt = blockStmt;
		blockStmt.EndLocation = t.EndLocation;
		compilationUnit.BlockEnd();
		
	}

	void VariableDeclarator(
#line  1462 "cs.ATG" 
ArrayList fieldDeclaration) {

#line  1463 "cs.ATG" 
		Expression expr = null; 
		Expect(1);

#line  1465 "cs.ATG" 
		VariableDeclaration f = new VariableDeclaration(t.val); 
		if (la.kind == 3) {
			lexer.NextToken();
			VariableInitializer(
#line  1466 "cs.ATG" 
out expr);

#line  1466 "cs.ATG" 
			f.Initializer = expr; 
		}

#line  1466 "cs.ATG" 
		fieldDeclaration.Add(f); 
	}

	void EventAccessorDecls(
#line  1411 "cs.ATG" 
out EventAddRegion addBlock, out EventRemoveRegion removeBlock) {

#line  1412 "cs.ATG" 
		AttributeSection section;
		ArrayList attributes = new ArrayList();
		Statement stmt;
		addBlock = null;
		removeBlock = null;
		
		while (la.kind == 17) {
			AttributeSection(
#line  1419 "cs.ATG" 
out section);

#line  1419 "cs.ATG" 
			attributes.Add(section); 
		}
		if (
#line  1421 "cs.ATG" 
IdentIsAdd()) {

#line  1421 "cs.ATG" 
			addBlock = new EventAddRegion(attributes); 
			AddAccessorDecl(
#line  1422 "cs.ATG" 
out stmt);

#line  1422 "cs.ATG" 
			attributes = new ArrayList(); addBlock.Block = (BlockStatement)stmt; 
			while (la.kind == 17) {
				AttributeSection(
#line  1423 "cs.ATG" 
out section);

#line  1423 "cs.ATG" 
				attributes.Add(section); 
			}
			RemoveAccessorDecl(
#line  1424 "cs.ATG" 
out stmt);

#line  1424 "cs.ATG" 
			removeBlock = new EventRemoveRegion(attributes); removeBlock.Block = (BlockStatement)stmt; 
		} else if (
#line  1425 "cs.ATG" 
IdentIsRemove()) {
			RemoveAccessorDecl(
#line  1426 "cs.ATG" 
out stmt);

#line  1426 "cs.ATG" 
			removeBlock = new EventRemoveRegion(attributes); removeBlock.Block = (BlockStatement)stmt; attributes = new ArrayList(); 
			while (la.kind == 17) {
				AttributeSection(
#line  1427 "cs.ATG" 
out section);

#line  1427 "cs.ATG" 
				attributes.Add(section); 
			}
			AddAccessorDecl(
#line  1428 "cs.ATG" 
out stmt);

#line  1428 "cs.ATG" 
			addBlock = new EventAddRegion(attributes); addBlock.Block = (BlockStatement)stmt; 
		} else if (la.kind == 1) {
			lexer.NextToken();

#line  1429 "cs.ATG" 
			Error("add or remove accessor declaration expected"); 
		} else SynErr(155);
	}

	void ConstructorInitializer(
#line  1500 "cs.ATG" 
out ConstructorInitializer ci) {

#line  1501 "cs.ATG" 
		Expression expr; ci = new ConstructorInitializer(); 
		Expect(9);
		if (la.kind == 49) {
			lexer.NextToken();

#line  1505 "cs.ATG" 
			ci.ConstructorInitializerType = ConstructorInitializerType.Base; 
		} else if (la.kind == 109) {
			lexer.NextToken();

#line  1506 "cs.ATG" 
			ci.ConstructorInitializerType = ConstructorInitializerType.This; 
		} else SynErr(156);
		Expect(19);
		if (StartOf(21)) {
			Argument(
#line  1509 "cs.ATG" 
out expr);

#line  1509 "cs.ATG" 
			if (expr != null) { ci.Arguments.Add(expr); } 
			while (la.kind == 13) {
				lexer.NextToken();
				Argument(
#line  1509 "cs.ATG" 
out expr);

#line  1509 "cs.ATG" 
				if (expr != null) { ci.Arguments.Add(expr); } 
			}
		}
		Expect(20);
	}

	void OverloadableOperator(
#line  1521 "cs.ATG" 
out OverloadableOperatorType op) {

#line  1522 "cs.ATG" 
		op = OverloadableOperatorType.None; 
		switch (la.kind) {
		case 4: {
			lexer.NextToken();

#line  1524 "cs.ATG" 
			op = OverloadableOperatorType.Add; 
			break;
		}
		case 5: {
			lexer.NextToken();

#line  1525 "cs.ATG" 
			op = OverloadableOperatorType.Subtract; 
			break;
		}
		case 23: {
			lexer.NextToken();

#line  1527 "cs.ATG" 
			op = OverloadableOperatorType.Not; 
			break;
		}
		case 26: {
			lexer.NextToken();

#line  1528 "cs.ATG" 
			op = OverloadableOperatorType.BitNot; 
			break;
		}
		case 30: {
			lexer.NextToken();

#line  1530 "cs.ATG" 
			op = OverloadableOperatorType.Increment; 
			break;
		}
		case 31: {
			lexer.NextToken();

#line  1531 "cs.ATG" 
			op = OverloadableOperatorType.Decrement; 
			break;
		}
		case 111: {
			lexer.NextToken();

#line  1533 "cs.ATG" 
			op = OverloadableOperatorType.True; 
			break;
		}
		case 70: {
			lexer.NextToken();

#line  1534 "cs.ATG" 
			op = OverloadableOperatorType.False; 
			break;
		}
		case 6: {
			lexer.NextToken();

#line  1536 "cs.ATG" 
			op = OverloadableOperatorType.Multiply; 
			break;
		}
		case 7: {
			lexer.NextToken();

#line  1537 "cs.ATG" 
			op = OverloadableOperatorType.Divide; 
			break;
		}
		case 8: {
			lexer.NextToken();

#line  1538 "cs.ATG" 
			op = OverloadableOperatorType.Modulus; 
			break;
		}
		case 27: {
			lexer.NextToken();

#line  1540 "cs.ATG" 
			op = OverloadableOperatorType.BitwiseAnd; 
			break;
		}
		case 28: {
			lexer.NextToken();

#line  1541 "cs.ATG" 
			op = OverloadableOperatorType.BitwiseOr; 
			break;
		}
		case 29: {
			lexer.NextToken();

#line  1542 "cs.ATG" 
			op = OverloadableOperatorType.ExclusiveOr; 
			break;
		}
		case 36: {
			lexer.NextToken();

#line  1544 "cs.ATG" 
			op = OverloadableOperatorType.ShiftLeft; 
			break;
		}
		case 32: {
			lexer.NextToken();

#line  1545 "cs.ATG" 
			op = OverloadableOperatorType.Equality; 
			break;
		}
		case 33: {
			lexer.NextToken();

#line  1546 "cs.ATG" 
			op = OverloadableOperatorType.InEquality; 
			break;
		}
		case 22: {
			lexer.NextToken();

#line  1547 "cs.ATG" 
			op = OverloadableOperatorType.LessThan; 
			break;
		}
		case 34: {
			lexer.NextToken();

#line  1548 "cs.ATG" 
			op = OverloadableOperatorType.GreaterThanOrEqual; 
			break;
		}
		case 35: {
			lexer.NextToken();

#line  1549 "cs.ATG" 
			op = OverloadableOperatorType.LessThanOrEqual; 
			break;
		}
		case 21: {
			lexer.NextToken();

#line  1550 "cs.ATG" 
			op = OverloadableOperatorType.GreaterThan; 
			if (la.kind == 21) {
				lexer.NextToken();

#line  1550 "cs.ATG" 
				op = OverloadableOperatorType.ShiftRight; 
			}
			break;
		}
		default: SynErr(157); break;
		}
	}

	void AccessorDecls(
#line  1369 "cs.ATG" 
out PropertyGetRegion getBlock, out PropertySetRegion setBlock) {

#line  1371 "cs.ATG" 
		ArrayList attributes = new ArrayList(); 
		AttributeSection section;
		getBlock = null;
		setBlock = null; 
		
		while (la.kind == 17) {
			AttributeSection(
#line  1377 "cs.ATG" 
out section);

#line  1377 "cs.ATG" 
			attributes.Add(section); 
		}
		if (
#line  1379 "cs.ATG" 
IdentIsGet()) {
			GetAccessorDecl(
#line  1380 "cs.ATG" 
out getBlock, attributes);
			if (la.kind == 1 || la.kind == 17) {

#line  1381 "cs.ATG" 
				attributes = new ArrayList(); 
				while (la.kind == 17) {
					AttributeSection(
#line  1382 "cs.ATG" 
out section);

#line  1382 "cs.ATG" 
					attributes.Add(section); 
				}
				SetAccessorDecl(
#line  1383 "cs.ATG" 
out setBlock, attributes);
			}
		} else if (
#line  1385 "cs.ATG" 
IdentIsSet()) {
			SetAccessorDecl(
#line  1386 "cs.ATG" 
out setBlock, attributes);
			if (la.kind == 1 || la.kind == 17) {

#line  1387 "cs.ATG" 
				attributes = new ArrayList(); 
				while (la.kind == 17) {
					AttributeSection(
#line  1388 "cs.ATG" 
out section);

#line  1388 "cs.ATG" 
					attributes.Add(section); 
				}
				GetAccessorDecl(
#line  1389 "cs.ATG" 
out getBlock, attributes);
			}
		} else if (la.kind == 1) {
			lexer.NextToken();

#line  1391 "cs.ATG" 
			Error("get or set accessor declaration expected"); 
		} else SynErr(158);
	}

	void InterfaceAccessors(
#line  1433 "cs.ATG" 
out PropertyGetRegion getBlock, out PropertySetRegion setBlock) {

#line  1435 "cs.ATG" 
		AttributeSection section;
		ArrayList attributes = new ArrayList();
		getBlock = null; setBlock = null;
		
		while (la.kind == 17) {
			AttributeSection(
#line  1440 "cs.ATG" 
out section);

#line  1440 "cs.ATG" 
			attributes.Add(section); 
		}
		if (
#line  1442 "cs.ATG" 
IdentIsGet()) {
			Expect(1);

#line  1442 "cs.ATG" 
			getBlock = new PropertyGetRegion(null, attributes); 
		} else if (
#line  1443 "cs.ATG" 
IdentIsSet()) {
			Expect(1);

#line  1443 "cs.ATG" 
			setBlock = new PropertySetRegion(null, attributes); 
		} else if (la.kind == 1) {
			lexer.NextToken();

#line  1444 "cs.ATG" 
			Error("set or get expected"); 
		} else SynErr(159);
		Expect(11);

#line  1446 "cs.ATG" 
		attributes = new ArrayList(); 
		if (la.kind == 1 || la.kind == 17) {
			while (la.kind == 17) {
				AttributeSection(
#line  1448 "cs.ATG" 
out section);

#line  1448 "cs.ATG" 
				attributes.Add(section); 
			}
			if (
#line  1450 "cs.ATG" 
IdentIsGet()) {
				Expect(1);

#line  1450 "cs.ATG" 
				if (getBlock != null) Error("get already declared");
				else getBlock = new PropertyGetRegion(null, attributes);
				
			} else if (
#line  1453 "cs.ATG" 
IdentIsSet()) {
				Expect(1);

#line  1453 "cs.ATG" 
				if (setBlock != null) Error("set already declared");
				else setBlock = new PropertySetRegion(null, attributes);
				
			} else if (la.kind == 1) {
				lexer.NextToken();

#line  1456 "cs.ATG" 
				Error("set or get expected"); 
			} else SynErr(160);
			Expect(11);
		}
	}

	void GetAccessorDecl(
#line  1395 "cs.ATG" 
out PropertyGetRegion getBlock, ArrayList attributes) {

#line  1396 "cs.ATG" 
		Statement stmt = null; 
		Expect(1);

#line  1399 "cs.ATG" 
		if (t.val != "get") Error("get expected"); 
		if (la.kind == 15) {
			Block(
#line  1400 "cs.ATG" 
out stmt);
		} else if (la.kind == 11) {
			lexer.NextToken();
		} else SynErr(161);

#line  1400 "cs.ATG" 
		getBlock = new PropertyGetRegion((BlockStatement)stmt, attributes); 
	}

	void SetAccessorDecl(
#line  1403 "cs.ATG" 
out PropertySetRegion setBlock, ArrayList attributes) {

#line  1404 "cs.ATG" 
		Statement stmt = null; 
		Expect(1);

#line  1407 "cs.ATG" 
		if (t.val != "set") Error("set expected"); 
		if (la.kind == 15) {
			Block(
#line  1408 "cs.ATG" 
out stmt);
		} else if (la.kind == 11) {
			lexer.NextToken();
		} else SynErr(162);

#line  1408 "cs.ATG" 
		setBlock = new PropertySetRegion((BlockStatement)stmt, attributes); 
	}

	void AddAccessorDecl(
#line  1484 "cs.ATG" 
out Statement stmt) {

#line  1485 "cs.ATG" 
		stmt = null;
		Expect(1);

#line  1488 "cs.ATG" 
		if (t.val != "add") Error("add expected"); 
		Block(
#line  1489 "cs.ATG" 
out stmt);
	}

	void RemoveAccessorDecl(
#line  1492 "cs.ATG" 
out Statement stmt) {

#line  1493 "cs.ATG" 
		stmt = null;
		Expect(1);

#line  1496 "cs.ATG" 
		if (t.val != "remove") Error("remove expected"); 
		Block(
#line  1497 "cs.ATG" 
out stmt);
	}

	void VariableInitializer(
#line  1513 "cs.ATG" 
out Expression initializerExpression) {

#line  1514 "cs.ATG" 
		TypeReference type = null; Expression expr = null; initializerExpression = null; 
		if (StartOf(4)) {
			Expr(
#line  1516 "cs.ATG" 
out initializerExpression);
		} else if (la.kind == 15) {
			ArrayInitializer(
#line  1517 "cs.ATG" 
out initializerExpression);
		} else if (la.kind == 104) {
			lexer.NextToken();
			Type(
#line  1518 "cs.ATG" 
out type);
			Expect(17);
			Expr(
#line  1518 "cs.ATG" 
out expr);
			Expect(18);

#line  1518 "cs.ATG" 
			initializerExpression = new StackAllocExpression(type, expr); 
		} else SynErr(163);
	}

	void Statement() {

#line  1622 "cs.ATG" 
		TypeReference type;
		Expression expr;
		Statement stmt;
		
		if (
#line  1628 "cs.ATG" 
IsLabel()) {
			Expect(1);

#line  1628 "cs.ATG" 
			compilationUnit.AddChild(new LabelStatement(t.val)); 
			Expect(9);
			Statement();
		} else if (la.kind == 58) {
			lexer.NextToken();
			Type(
#line  1631 "cs.ATG" 
out type);

#line  1631 "cs.ATG" 
			LocalVariableDeclaration var = new LocalVariableDeclaration(type, Modifier.Const); string ident = null; var.StartLocation = t.Location; 
			Expect(1);

#line  1632 "cs.ATG" 
			ident = t.val; 
			Expect(3);
			Expr(
#line  1633 "cs.ATG" 
out expr);

#line  1633 "cs.ATG" 
			var.Variables.Add(new VariableDeclaration(ident, expr)); 
			while (la.kind == 13) {
				lexer.NextToken();
				Expect(1);

#line  1634 "cs.ATG" 
				ident = t.val; 
				Expect(3);
				Expr(
#line  1634 "cs.ATG" 
out expr);

#line  1634 "cs.ATG" 
				var.Variables.Add(new VariableDeclaration(ident, expr)); 
			}
			Expect(11);

#line  1635 "cs.ATG" 
			compilationUnit.AddChild(var); 
		} else if (
#line  1637 "cs.ATG" 
IsLocalVarDecl()) {
			LocalVariableDecl(
#line  1637 "cs.ATG" 
out stmt);
			Expect(11);

#line  1637 "cs.ATG" 
			compilationUnit.AddChild(stmt); 
		} else if (StartOf(22)) {
			EmbeddedStatement(
#line  1638 "cs.ATG" 
out stmt);

#line  1638 "cs.ATG" 
			compilationUnit.AddChild(stmt); 
		} else SynErr(164);
	}

	void Argument(
#line  1553 "cs.ATG" 
out Expression argumentexpr) {

#line  1555 "cs.ATG" 
		Expression expr;
		FieldDirection fd = FieldDirection.None;
		
		if (la.kind == 91 || la.kind == 98) {
			if (la.kind == 98) {
				lexer.NextToken();

#line  1560 "cs.ATG" 
				fd = FieldDirection.Ref; 
			} else {
				lexer.NextToken();

#line  1561 "cs.ATG" 
				fd = FieldDirection.Out; 
			}
		}
		Expr(
#line  1563 "cs.ATG" 
out expr);

#line  1563 "cs.ATG" 
		argumentexpr = fd != FieldDirection.None ? argumentexpr = new DirectionExpression(fd, expr) : expr; 
	}

	void ArrayInitializer(
#line  1582 "cs.ATG" 
out Expression outExpr) {

#line  1584 "cs.ATG" 
		Expression expr = null;
		ArrayInitializerExpression initializer = new ArrayInitializerExpression();
		
		Expect(15);
		if (StartOf(23)) {
			VariableInitializer(
#line  1589 "cs.ATG" 
out expr);

#line  1589 "cs.ATG" 
			if (expr != null) { initializer.CreateExpressions.Add(expr); } 
			while (
#line  1589 "cs.ATG" 
NotFinalComma()) {
				Expect(13);
				VariableInitializer(
#line  1589 "cs.ATG" 
out expr);

#line  1589 "cs.ATG" 
				if (expr != null) { initializer.CreateExpressions.Add(expr); } 
			}
			if (la.kind == 13) {
				lexer.NextToken();
			}
		}
		Expect(16);

#line  1590 "cs.ATG" 
		outExpr = initializer; 
	}

	void AssignmentOperator(
#line  1566 "cs.ATG" 
out AssignmentOperatorType op) {

#line  1567 "cs.ATG" 
		op = AssignmentOperatorType.None; 
		switch (la.kind) {
		case 3: {
			lexer.NextToken();

#line  1569 "cs.ATG" 
			op = AssignmentOperatorType.Assign; 
			break;
		}
		case 37: {
			lexer.NextToken();

#line  1570 "cs.ATG" 
			op = AssignmentOperatorType.Add; 
			break;
		}
		case 38: {
			lexer.NextToken();

#line  1571 "cs.ATG" 
			op = AssignmentOperatorType.Subtract; 
			break;
		}
		case 39: {
			lexer.NextToken();

#line  1572 "cs.ATG" 
			op = AssignmentOperatorType.Multiply; 
			break;
		}
		case 40: {
			lexer.NextToken();

#line  1573 "cs.ATG" 
			op = AssignmentOperatorType.Divide; 
			break;
		}
		case 41: {
			lexer.NextToken();

#line  1574 "cs.ATG" 
			op = AssignmentOperatorType.Modulus; 
			break;
		}
		case 42: {
			lexer.NextToken();

#line  1575 "cs.ATG" 
			op = AssignmentOperatorType.BitwiseAnd; 
			break;
		}
		case 43: {
			lexer.NextToken();

#line  1576 "cs.ATG" 
			op = AssignmentOperatorType.BitwiseOr; 
			break;
		}
		case 44: {
			lexer.NextToken();

#line  1577 "cs.ATG" 
			op = AssignmentOperatorType.ExclusiveOr; 
			break;
		}
		case 45: {
			lexer.NextToken();

#line  1578 "cs.ATG" 
			op = AssignmentOperatorType.ShiftLeft; 
			break;
		}
		case 21: {
			lexer.NextToken();
			Expect(34);

#line  1579 "cs.ATG" 
			op = AssignmentOperatorType.ShiftRight; 
			break;
		}
		default: SynErr(165); break;
		}
	}

	void LocalVariableDecl(
#line  1593 "cs.ATG" 
out Statement stmt) {

#line  1595 "cs.ATG" 
		TypeReference type;
		VariableDeclaration      var = null;
		LocalVariableDeclaration localVariableDeclaration; 
		
		Type(
#line  1600 "cs.ATG" 
out type);

#line  1600 "cs.ATG" 
		localVariableDeclaration = new LocalVariableDeclaration(type); localVariableDeclaration.StartLocation = t.Location; 
		LocalVariableDeclarator(
#line  1601 "cs.ATG" 
out var);

#line  1601 "cs.ATG" 
		localVariableDeclaration.Variables.Add(var); 
		while (la.kind == 13) {
			lexer.NextToken();
			LocalVariableDeclarator(
#line  1602 "cs.ATG" 
out var);

#line  1602 "cs.ATG" 
			localVariableDeclaration.Variables.Add(var); 
		}

#line  1603 "cs.ATG" 
		stmt = localVariableDeclaration; 
	}

	void LocalVariableDeclarator(
#line  1606 "cs.ATG" 
out VariableDeclaration var) {

#line  1607 "cs.ATG" 
		Expression expr = null; 
		Expect(1);

#line  1610 "cs.ATG" 
		var = new VariableDeclaration(t.val); 
		if (la.kind == 3) {
			lexer.NextToken();
			VariableInitializer(
#line  1610 "cs.ATG" 
out expr);

#line  1610 "cs.ATG" 
			var.Initializer = expr; 
		}
	}

	void EmbeddedStatement(
#line  1644 "cs.ATG" 
out Statement statement) {

#line  1646 "cs.ATG" 
		TypeReference type = null;
		Expression expr = null;
		Statement embeddedStatement = null;
		statement = null;
		
		if (la.kind == 15) {
			Block(
#line  1652 "cs.ATG" 
out statement);
		} else if (la.kind == 11) {
			lexer.NextToken();

#line  1654 "cs.ATG" 
			statement = new EmptyStatement(); 
		} else if (
#line  1656 "cs.ATG" 
UnCheckedAndLBrace()) {

#line  1656 "cs.ATG" 
			Statement block; bool isChecked = true; 
			if (la.kind == 56) {
				lexer.NextToken();
			} else if (la.kind == 116) {
				lexer.NextToken();

#line  1657 "cs.ATG" 
				isChecked = false;
			} else SynErr(166);
			Block(
#line  1658 "cs.ATG" 
out block);

#line  1658 "cs.ATG" 
			statement = isChecked ? (Statement)new CheckedStatement(block) : (Statement)new UncheckedStatement(block); 
		} else if (la.kind == 77) {
			lexer.NextToken();

#line  1660 "cs.ATG" 
			Statement elseStatement = null; 
			Expect(19);
			Expr(
#line  1661 "cs.ATG" 
out expr);
			Expect(20);
			EmbeddedStatement(
#line  1662 "cs.ATG" 
out embeddedStatement);
			if (la.kind == 65) {
				lexer.NextToken();
				EmbeddedStatement(
#line  1663 "cs.ATG" 
out elseStatement);
			}

#line  1664 "cs.ATG" 
			statement = elseStatement != null ? (Statement)new IfElseStatement(expr, embeddedStatement, elseStatement) :  (Statement)new IfElseStatement(expr, embeddedStatement); 
		} else if (la.kind == 108) {
			lexer.NextToken();

#line  1665 "cs.ATG" 
			ArrayList switchSections = new ArrayList(); SwitchSection switchSection; 
			Expect(19);
			Expr(
#line  1666 "cs.ATG" 
out expr);
			Expect(20);
			Expect(15);
			while (la.kind == 53 || la.kind == 61) {
				SwitchSection(
#line  1667 "cs.ATG" 
out switchSection);

#line  1667 "cs.ATG" 
				switchSections.Add(switchSection); 
			}
			Expect(16);

#line  1668 "cs.ATG" 
			statement = new SwitchStatement(expr, switchSections); 
		} else if (la.kind == 123) {
			lexer.NextToken();
			Expect(19);
			Expr(
#line  1670 "cs.ATG" 
out expr);
			Expect(20);
			EmbeddedStatement(
#line  1672 "cs.ATG" 
out embeddedStatement);

#line  1672 "cs.ATG" 
			statement = new DoLoopStatement(expr, embeddedStatement, ConditionType.While, ConditionPosition.Start);
		} else if (la.kind == 63) {
			lexer.NextToken();
			EmbeddedStatement(
#line  1673 "cs.ATG" 
out embeddedStatement);
			Expect(123);
			Expect(19);
			Expr(
#line  1674 "cs.ATG" 
out expr);
			Expect(20);
			Expect(11);

#line  1674 "cs.ATG" 
			statement = new DoLoopStatement(expr, embeddedStatement, ConditionType.While, ConditionPosition.End); 
		} else if (la.kind == 74) {
			lexer.NextToken();

#line  1675 "cs.ATG" 
			ArrayList initializer = null; ArrayList iterator = null; 
			Expect(19);
			if (StartOf(4)) {
				ForInitializer(
#line  1676 "cs.ATG" 
out initializer);
			}
			Expect(11);
			if (StartOf(4)) {
				Expr(
#line  1677 "cs.ATG" 
out expr);
			}
			Expect(11);
			if (StartOf(4)) {
				ForIterator(
#line  1678 "cs.ATG" 
out iterator);
			}
			Expect(20);
			EmbeddedStatement(
#line  1679 "cs.ATG" 
out embeddedStatement);

#line  1679 "cs.ATG" 
			statement = new ForStatement(initializer, expr, iterator, embeddedStatement); 
		} else if (la.kind == 75) {
			lexer.NextToken();
			Expect(19);
			Type(
#line  1680 "cs.ATG" 
out type);
			Expect(1);

#line  1680 "cs.ATG" 
			string varName = t.val; Point start = t.Location;
			Expect(79);
			Expr(
#line  1681 "cs.ATG" 
out expr);
			Expect(20);
			EmbeddedStatement(
#line  1682 "cs.ATG" 
out embeddedStatement);

#line  1682 "cs.ATG" 
			statement = new ForeachStatement(type, varName , expr, embeddedStatement); 
			statement.EndLocation = t.EndLocation;
			
		} else if (la.kind == 51) {
			lexer.NextToken();
			Expect(11);

#line  1686 "cs.ATG" 
			statement = new BreakStatement(); 
		} else if (la.kind == 59) {
			lexer.NextToken();
			Expect(11);

#line  1687 "cs.ATG" 
			statement = new ContinueStatement(); 
		} else if (la.kind == 76) {
			GotoStatement(
#line  1688 "cs.ATG" 
out statement);
		} else if (
#line  1689 "cs.ATG" 
IsYieldStatement()) {
			Expect(1);
			if (la.kind == 99) {
				lexer.NextToken();
				Expr(
#line  1689 "cs.ATG" 
out expr);

#line  1689 "cs.ATG" 
				statement = new YieldStatement(new ReturnStatement(expr)); 
			} else if (la.kind == 51) {
				lexer.NextToken();

#line  1690 "cs.ATG" 
				statement = new YieldStatement(new BreakStatement()); 
			} else SynErr(167);
			Expect(11);
		} else if (la.kind == 99) {
			lexer.NextToken();
			if (StartOf(4)) {
				Expr(
#line  1691 "cs.ATG" 
out expr);
			}
			Expect(11);

#line  1691 "cs.ATG" 
			statement = new ReturnStatement(expr); 
		} else if (la.kind == 110) {
			lexer.NextToken();
			if (StartOf(4)) {
				Expr(
#line  1692 "cs.ATG" 
out expr);
			}
			Expect(11);

#line  1692 "cs.ATG" 
			statement = new ThrowStatement(expr); 
		} else if (StartOf(4)) {
			StatementExpr(
#line  1694 "cs.ATG" 
out statement);
			Expect(11);
		} else if (la.kind == 112) {
			TryStatement(
#line  1696 "cs.ATG" 
out statement);
		} else if (la.kind == 84) {
			lexer.NextToken();
			Expect(19);
			Expr(
#line  1698 "cs.ATG" 
out expr);
			Expect(20);
			EmbeddedStatement(
#line  1699 "cs.ATG" 
out embeddedStatement);

#line  1699 "cs.ATG" 
			statement = new LockStatement(expr, embeddedStatement); 
		} else if (la.kind == 119) {

#line  1701 "cs.ATG" 
			Statement resourceAcquisitionStmt = null; 
			lexer.NextToken();
			Expect(19);
			ResourceAcquisition(
#line  1703 "cs.ATG" 
out resourceAcquisitionStmt);
			Expect(20);
			EmbeddedStatement(
#line  1704 "cs.ATG" 
out embeddedStatement);

#line  1704 "cs.ATG" 
			statement = new UsingStatement(resourceAcquisitionStmt, embeddedStatement); 
		} else if (la.kind == 117) {
			lexer.NextToken();
			Block(
#line  1706 "cs.ATG" 
out embeddedStatement);

#line  1706 "cs.ATG" 
			statement = new UnsafeStatement(embeddedStatement); 
		} else if (la.kind == 72) {
			lexer.NextToken();
			Expect(19);
			Type(
#line  1709 "cs.ATG" 
out type);

#line  1709 "cs.ATG" 
			if (type.PointerNestingLevel == 0) Error("can only fix pointer types");
			ArrayList pointerDeclarators = new ArrayList(1);
			
			Expect(1);

#line  1712 "cs.ATG" 
			string identifier = t.val; 
			Expect(3);
			Expr(
#line  1713 "cs.ATG" 
out expr);

#line  1713 "cs.ATG" 
			pointerDeclarators.Add(new VariableDeclaration(identifier, expr)); 
			while (la.kind == 13) {
				lexer.NextToken();
				Expect(1);

#line  1715 "cs.ATG" 
				identifier = t.val; 
				Expect(3);
				Expr(
#line  1716 "cs.ATG" 
out expr);

#line  1716 "cs.ATG" 
				pointerDeclarators.Add(new VariableDeclaration(identifier, expr)); 
			}
			Expect(20);
			EmbeddedStatement(
#line  1718 "cs.ATG" 
out embeddedStatement);

#line  1718 "cs.ATG" 
			statement = new FixedStatement(type, pointerDeclarators, embeddedStatement); 
		} else SynErr(168);
	}

	void SwitchSection(
#line  1740 "cs.ATG" 
out SwitchSection stmt) {

#line  1742 "cs.ATG" 
		SwitchSection switchSection = new SwitchSection();
		CaseLabel label;
		
		SwitchLabel(
#line  1746 "cs.ATG" 
out label);

#line  1746 "cs.ATG" 
		switchSection.SwitchLabels.Add(label); 
		while (la.kind == 53 || la.kind == 61) {
			SwitchLabel(
#line  1748 "cs.ATG" 
out label);

#line  1748 "cs.ATG" 
			switchSection.SwitchLabels.Add(label); 
		}

#line  1750 "cs.ATG" 
		compilationUnit.BlockStart(switchSection); 
		Statement();
		while (StartOf(20)) {
			Statement();
		}

#line  1753 "cs.ATG" 
		compilationUnit.BlockEnd();
		stmt = switchSection;
		
	}

	void ForInitializer(
#line  1721 "cs.ATG" 
out ArrayList initializer) {

#line  1723 "cs.ATG" 
		Statement stmt; 
		initializer = new ArrayList();
		
		if (
#line  1727 "cs.ATG" 
IsLocalVarDecl()) {
			LocalVariableDecl(
#line  1727 "cs.ATG" 
out stmt);

#line  1727 "cs.ATG" 
			initializer.Add(stmt);
		} else if (StartOf(4)) {
			StatementExpr(
#line  1728 "cs.ATG" 
out stmt);

#line  1728 "cs.ATG" 
			initializer.Add(stmt);
			while (la.kind == 13) {
				lexer.NextToken();
				StatementExpr(
#line  1728 "cs.ATG" 
out stmt);

#line  1728 "cs.ATG" 
				initializer.Add(stmt);
			}

#line  1728 "cs.ATG" 
			initializer.Add(stmt);
		} else SynErr(169);
	}

	void ForIterator(
#line  1731 "cs.ATG" 
out ArrayList iterator) {

#line  1733 "cs.ATG" 
		Statement stmt; 
		iterator = new ArrayList();
		
		StatementExpr(
#line  1737 "cs.ATG" 
out stmt);

#line  1737 "cs.ATG" 
		iterator.Add(stmt);
		while (la.kind == 13) {
			lexer.NextToken();
			StatementExpr(
#line  1737 "cs.ATG" 
out stmt);

#line  1737 "cs.ATG" 
			iterator.Add(stmt); 
		}
	}

	void GotoStatement(
#line  1803 "cs.ATG" 
out Statement stmt) {

#line  1804 "cs.ATG" 
		Expression expr; stmt = null; 
		Expect(76);
		if (la.kind == 1) {
			lexer.NextToken();

#line  1808 "cs.ATG" 
			stmt = new GotoStatement(t.val); 
			Expect(11);
		} else if (la.kind == 53) {
			lexer.NextToken();
			Expr(
#line  1809 "cs.ATG" 
out expr);
			Expect(11);

#line  1809 "cs.ATG" 
			stmt = new GotoCaseStatement(expr); 
		} else if (la.kind == 61) {
			lexer.NextToken();
			Expect(11);

#line  1810 "cs.ATG" 
			stmt = new GotoCaseStatement(null); 
		} else SynErr(170);
	}

	void StatementExpr(
#line  1830 "cs.ATG" 
out Statement stmt) {

#line  1835 "cs.ATG" 
		bool mustBeAssignment = la.kind == Tokens.Plus  || la.kind == Tokens.Minus ||
		                       la.kind == Tokens.Not   || la.kind == Tokens.BitwiseComplement ||
		                       la.kind == Tokens.Times || la.kind == Tokens.BitwiseAnd   || IsTypeCast();
		Expression expr = null;
		
		UnaryExpr(
#line  1841 "cs.ATG" 
out expr);
		if (StartOf(6)) {

#line  1844 "cs.ATG" 
			AssignmentOperatorType op; Expression val; 
			AssignmentOperator(
#line  1844 "cs.ATG" 
out op);
			Expr(
#line  1844 "cs.ATG" 
out val);

#line  1844 "cs.ATG" 
			expr = new AssignmentExpression(expr, op, val); 
		} else if (la.kind == 11 || la.kind == 13 || la.kind == 20) {

#line  1845 "cs.ATG" 
			if (mustBeAssignment) Error("error in assignment."); 
		} else SynErr(171);

#line  1846 "cs.ATG" 
		stmt = new StatementExpression(expr); 
	}

	void TryStatement(
#line  1765 "cs.ATG" 
out Statement tryStatement) {

#line  1767 "cs.ATG" 
		Statement blockStmt = null, finallyStmt = null;
		ArrayList catchClauses = null;
		
		Expect(112);
		Block(
#line  1771 "cs.ATG" 
out blockStmt);
		if (la.kind == 54) {
			CatchClauses(
#line  1773 "cs.ATG" 
out catchClauses);
			if (la.kind == 71) {
				lexer.NextToken();
				Block(
#line  1773 "cs.ATG" 
out finallyStmt);
			}
		} else if (la.kind == 71) {
			lexer.NextToken();
			Block(
#line  1774 "cs.ATG" 
out finallyStmt);
		} else SynErr(172);

#line  1777 "cs.ATG" 
		tryStatement = new TryCatchStatement(blockStmt, catchClauses, finallyStmt);
			
	}

	void ResourceAcquisition(
#line  1814 "cs.ATG" 
out Statement stmt) {

#line  1816 "cs.ATG" 
		stmt = null;
		Expression expr;
		
		if (
#line  1821 "cs.ATG" 
IsLocalVarDecl()) {
			LocalVariableDecl(
#line  1821 "cs.ATG" 
out stmt);
		} else if (StartOf(4)) {
			Expr(
#line  1822 "cs.ATG" 
out expr);

#line  1826 "cs.ATG" 
			stmt = new StatementExpression(expr); 
		} else SynErr(173);
	}

	void SwitchLabel(
#line  1758 "cs.ATG" 
out CaseLabel label) {

#line  1759 "cs.ATG" 
		Expression expr = null; label = null; 
		if (la.kind == 53) {
			lexer.NextToken();
			Expr(
#line  1761 "cs.ATG" 
out expr);
			Expect(9);

#line  1761 "cs.ATG" 
			label =  new CaseLabel(expr); 
		} else if (la.kind == 61) {
			lexer.NextToken();
			Expect(9);

#line  1762 "cs.ATG" 
			label =  new CaseLabel(); 
		} else SynErr(174);
	}

	void CatchClauses(
#line  1782 "cs.ATG" 
out ArrayList catchClauses) {

#line  1784 "cs.ATG" 
		catchClauses = new ArrayList();
		
		Expect(54);

#line  1787 "cs.ATG" 
		string name;
		string identifier;
		Statement stmt;
		List<TypeReference> types;
		
		if (la.kind == 15) {
			Block(
#line  1794 "cs.ATG" 
out stmt);

#line  1794 "cs.ATG" 
			catchClauses.Add(new CatchClause(stmt)); 
		} else if (la.kind == 19) {
			lexer.NextToken();
			ClassType(
#line  1796 "cs.ATG" 
out name, out types);

#line  1796 "cs.ATG" 
			identifier = null; 
			if (la.kind == 1) {
				lexer.NextToken();

#line  1796 "cs.ATG" 
				identifier = t.val; 
			}
			Expect(20);
			Block(
#line  1796 "cs.ATG" 
out stmt);

#line  1796 "cs.ATG" 
			catchClauses.Add(new CatchClause(new TypeReference(name, 0, null, types), identifier, stmt)); 
			while (
#line  1797 "cs.ATG" 
IsTypedCatch()) {
				Expect(54);
				Expect(19);
				ClassType(
#line  1797 "cs.ATG" 
out name, out types);

#line  1797 "cs.ATG" 
				identifier = null; 
				if (la.kind == 1) {
					lexer.NextToken();

#line  1797 "cs.ATG" 
					identifier = t.val; 
				}
				Expect(20);
				Block(
#line  1797 "cs.ATG" 
out stmt);

#line  1797 "cs.ATG" 
				catchClauses.Add(new CatchClause(new TypeReference(name, 0, null, types), identifier, stmt)); 
			}
			if (la.kind == 54) {
				lexer.NextToken();
				Block(
#line  1799 "cs.ATG" 
out stmt);

#line  1799 "cs.ATG" 
				catchClauses.Add(new CatchClause(stmt)); 
			}
		} else SynErr(175);
	}

	void UnaryExpr(
#line  1862 "cs.ATG" 
out Expression uExpr) {

#line  1864 "cs.ATG" 
		TypeReference type = null;
		Expression expr;
		ArrayList  expressions = new ArrayList();
		uExpr = null;
		
		while (StartOf(24) || 
#line  1888 "cs.ATG" 
IsTypeCast()) {
			if (la.kind == 4) {
				lexer.NextToken();

#line  1873 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Plus)); 
			} else if (la.kind == 5) {
				lexer.NextToken();

#line  1874 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Minus)); 
			} else if (la.kind == 23) {
				lexer.NextToken();

#line  1875 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Not)); 
			} else if (la.kind == 26) {
				lexer.NextToken();

#line  1876 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.BitNot)); 
			} else if (la.kind == 6) {
				lexer.NextToken();

#line  1877 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Star)); 
			} else if (la.kind == 30) {
				lexer.NextToken();

#line  1878 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Increment)); 
			} else if (la.kind == 31) {
				lexer.NextToken();

#line  1879 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.Decrement)); 
			} else if (la.kind == 27) {
				lexer.NextToken();

#line  1880 "cs.ATG" 
				expressions.Add(new UnaryOperatorExpression(UnaryOperatorType.BitWiseAnd)); 
			} else {
				Expect(19);
				Type(
#line  1888 "cs.ATG" 
out type);
				Expect(20);

#line  1888 "cs.ATG" 
				expressions.Add(new CastExpression(type)); 
			}
		}
		PrimaryExpr(
#line  1892 "cs.ATG" 
out expr);

#line  1892 "cs.ATG" 
		for (int i = 0; i < expressions.Count; ++i) {
		Expression nextExpression = i + 1 < expressions.Count ? (Expression)expressions[i + 1] : expr;
		if (expressions[i] is CastExpression) {
			((CastExpression)expressions[i]).Expression = nextExpression;
		} else {
			((UnaryOperatorExpression)expressions[i]).Expression = nextExpression;
		}
		}
		if (expressions.Count > 0) {
			uExpr = (Expression)expressions[0];
		} else {
			uExpr = expr;
		}
		
	}

	void ConditionalOrExpr(
#line  2063 "cs.ATG" 
ref Expression outExpr) {

#line  2064 "cs.ATG" 
		Expression expr;   
		ConditionalAndExpr(
#line  2066 "cs.ATG" 
ref outExpr);
		while (la.kind == 25) {
			lexer.NextToken();
			UnaryExpr(
#line  2066 "cs.ATG" 
out expr);
			ConditionalAndExpr(
#line  2066 "cs.ATG" 
ref expr);

#line  2066 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.LogicalOr, expr);  
		}
	}

	void PrimaryExpr(
#line  1909 "cs.ATG" 
out Expression pexpr) {

#line  1911 "cs.ATG" 
		TypeReference type = null;
		List<TypeReference> typeList = null;
		bool isArrayCreation = false;
		Expression expr;
		pexpr = null;
		
		switch (la.kind) {
		case 111: {
			lexer.NextToken();

#line  1919 "cs.ATG" 
			pexpr = new PrimitiveExpression(true, "true");  
			break;
		}
		case 70: {
			lexer.NextToken();

#line  1920 "cs.ATG" 
			pexpr = new PrimitiveExpression(false, "false"); 
			break;
		}
		case 88: {
			lexer.NextToken();

#line  1921 "cs.ATG" 
			pexpr = new PrimitiveExpression(null, "null");  
			break;
		}
		case 2: {
			lexer.NextToken();

#line  1922 "cs.ATG" 
			pexpr = new PrimitiveExpression(t.literalValue, t.val);  
			break;
		}
		case 1: {
			lexer.NextToken();

#line  1924 "cs.ATG" 
			pexpr = new IdentifierExpression(t.val); 
			break;
		}
		case 19: {
			lexer.NextToken();
			Expr(
#line  1926 "cs.ATG" 
out expr);
			Expect(20);

#line  1926 "cs.ATG" 
			pexpr = new ParenthesizedExpression(expr); 
			break;
		}
		case 50: case 52: case 55: case 60: case 64: case 73: case 80: case 85: case 89: case 100: case 102: case 106: case 114: case 115: case 118: {

#line  1928 "cs.ATG" 
			string val = null; 
			switch (la.kind) {
			case 50: {
				lexer.NextToken();

#line  1930 "cs.ATG" 
				val = "bool"; 
				break;
			}
			case 52: {
				lexer.NextToken();

#line  1931 "cs.ATG" 
				val = "byte"; 
				break;
			}
			case 55: {
				lexer.NextToken();

#line  1932 "cs.ATG" 
				val = "char"; 
				break;
			}
			case 60: {
				lexer.NextToken();

#line  1933 "cs.ATG" 
				val = "decimal"; 
				break;
			}
			case 64: {
				lexer.NextToken();

#line  1934 "cs.ATG" 
				val = "double"; 
				break;
			}
			case 73: {
				lexer.NextToken();

#line  1935 "cs.ATG" 
				val = "float"; 
				break;
			}
			case 80: {
				lexer.NextToken();

#line  1936 "cs.ATG" 
				val = "int"; 
				break;
			}
			case 85: {
				lexer.NextToken();

#line  1937 "cs.ATG" 
				val = "long"; 
				break;
			}
			case 89: {
				lexer.NextToken();

#line  1938 "cs.ATG" 
				val = "object"; 
				break;
			}
			case 100: {
				lexer.NextToken();

#line  1939 "cs.ATG" 
				val = "sbyte"; 
				break;
			}
			case 102: {
				lexer.NextToken();

#line  1940 "cs.ATG" 
				val = "short"; 
				break;
			}
			case 106: {
				lexer.NextToken();

#line  1941 "cs.ATG" 
				val = "string"; 
				break;
			}
			case 114: {
				lexer.NextToken();

#line  1942 "cs.ATG" 
				val = "uint"; 
				break;
			}
			case 115: {
				lexer.NextToken();

#line  1943 "cs.ATG" 
				val = "ulong"; 
				break;
			}
			case 118: {
				lexer.NextToken();

#line  1944 "cs.ATG" 
				val = "ushort"; 
				break;
			}
			}

#line  1945 "cs.ATG" 
			t.val = ""; 
			Expect(14);
			Expect(1);

#line  1945 "cs.ATG" 
			pexpr = new FieldReferenceExpression(new TypeReferenceExpression(val), t.val); 
			break;
		}
		case 109: {
			lexer.NextToken();

#line  1947 "cs.ATG" 
			pexpr = new ThisReferenceExpression(); 
			break;
		}
		case 49: {
			lexer.NextToken();

#line  1949 "cs.ATG" 
			Expression retExpr = new BaseReferenceExpression(); 
			if (la.kind == 14) {
				lexer.NextToken();
				Expect(1);

#line  1951 "cs.ATG" 
				retExpr = new FieldReferenceExpression(retExpr, t.val); 
			} else if (la.kind == 17) {
				lexer.NextToken();
				Expr(
#line  1952 "cs.ATG" 
out expr);

#line  1952 "cs.ATG" 
				ArrayList indices = new ArrayList(); if (expr != null) { indices.Add(expr); } 
				while (la.kind == 13) {
					lexer.NextToken();
					Expr(
#line  1953 "cs.ATG" 
out expr);

#line  1953 "cs.ATG" 
					if (expr != null) { indices.Add(expr); } 
				}
				Expect(18);

#line  1954 "cs.ATG" 
				retExpr = new IndexerExpression(retExpr, indices); 
			} else SynErr(176);

#line  1955 "cs.ATG" 
			pexpr = retExpr; 
			break;
		}
		case 87: {
			lexer.NextToken();
			NonArrayType(
#line  1956 "cs.ATG" 
out type);

#line  1956 "cs.ATG" 
			ArrayList parameters = new ArrayList(); 
			if (la.kind == 19) {
				lexer.NextToken();

#line  1961 "cs.ATG" 
				ObjectCreateExpression oce = new ObjectCreateExpression(type, parameters); 
				if (StartOf(21)) {
					Argument(
#line  1962 "cs.ATG" 
out expr);

#line  1962 "cs.ATG" 
					if (expr != null) { parameters.Add(expr); } 
					while (la.kind == 13) {
						lexer.NextToken();
						Argument(
#line  1963 "cs.ATG" 
out expr);

#line  1963 "cs.ATG" 
						if (expr != null) { parameters.Add(expr); } 
					}
				}
				Expect(20);

#line  1965 "cs.ATG" 
				pexpr = oce; 
			} else if (la.kind == 17) {

#line  1967 "cs.ATG" 
				isArrayCreation = true; ArrayCreateExpression ace = new ArrayCreateExpression(type); pexpr = ace; 
				lexer.NextToken();

#line  1968 "cs.ATG" 
				int dims = 0; 
				ArrayList rank = new ArrayList(); 
				ArrayList parameterExpression = new ArrayList(); 
				if (StartOf(4)) {
					Expr(
#line  1972 "cs.ATG" 
out expr);

#line  1972 "cs.ATG" 
					if (expr != null) { parameterExpression.Add(expr); } 
					while (la.kind == 13) {
						lexer.NextToken();
						Expr(
#line  1974 "cs.ATG" 
out expr);

#line  1974 "cs.ATG" 
						if (expr != null) { parameterExpression.Add(expr); } 
					}
					Expect(18);

#line  1976 "cs.ATG" 
					parameters.Add(new ArrayCreationParameter(parameterExpression)); 
					ace.Parameters = parameters; 
					while (
#line  1979 "cs.ATG" 
IsDims()) {
						Expect(17);

#line  1979 "cs.ATG" 
						dims =0;
						while (la.kind == 13) {
							lexer.NextToken();

#line  1980 "cs.ATG" 
							dims++;
						}

#line  1980 "cs.ATG" 
						rank.Add(dims); 
						parameters.Add(new ArrayCreationParameter(dims)); 
						
						Expect(18);
					}

#line  1984 "cs.ATG" 
					if (rank.Count > 0) { 
					ace.Rank = (int[])rank.ToArray(typeof (int)); 
					} 
					
					if (la.kind == 15) {
						ArrayInitializer(
#line  1988 "cs.ATG" 
out expr);

#line  1988 "cs.ATG" 
						ace.ArrayInitializer = (ArrayInitializerExpression)expr; 
					}
				} else if (la.kind == 13 || la.kind == 18) {
					while (la.kind == 13) {
						lexer.NextToken();

#line  1990 "cs.ATG" 
						dims++;
					}

#line  1991 "cs.ATG" 
					parameters.Add(new ArrayCreationParameter(dims)); 
					
					Expect(18);
					while (
#line  1993 "cs.ATG" 
IsDims()) {
						Expect(17);

#line  1993 "cs.ATG" 
						dims =0;
						while (la.kind == 13) {
							lexer.NextToken();

#line  1993 "cs.ATG" 
							dims++;
						}

#line  1993 "cs.ATG" 
						parameters.Add(new ArrayCreationParameter(dims)); 
						Expect(18);
					}
					ArrayInitializer(
#line  1993 "cs.ATG" 
out expr);

#line  1993 "cs.ATG" 
					ace.ArrayInitializer = (ArrayInitializerExpression)expr; ace.Parameters = parameters; 
				} else SynErr(177);
			} else SynErr(178);
			break;
		}
		case 113: {
			lexer.NextToken();
			Expect(19);
			if (
#line  1999 "cs.ATG" 
NotVoidPointer()) {
				Expect(121);

#line  1999 "cs.ATG" 
				type = new TypeReference("void"); 
			} else if (StartOf(8)) {
				Type(
#line  2000 "cs.ATG" 
out type);
			} else SynErr(179);
			Expect(20);

#line  2001 "cs.ATG" 
			pexpr = new TypeOfExpression(type); 
			break;
		}
		case 103: {
			lexer.NextToken();
			Expect(19);
			Type(
#line  2002 "cs.ATG" 
out type);
			Expect(20);

#line  2002 "cs.ATG" 
			pexpr = new SizeOfExpression(type); 
			break;
		}
		case 56: {
			lexer.NextToken();
			Expect(19);
			Expr(
#line  2003 "cs.ATG" 
out expr);
			Expect(20);

#line  2003 "cs.ATG" 
			pexpr = new CheckedExpression(expr); 
			break;
		}
		case 116: {
			lexer.NextToken();
			Expect(19);
			Expr(
#line  2004 "cs.ATG" 
out expr);
			Expect(20);

#line  2004 "cs.ATG" 
			pexpr = new UncheckedExpression(expr); 
			break;
		}
		case 62: {
			lexer.NextToken();
			AnonymousMethodExpr(
#line  2005 "cs.ATG" 
out expr);

#line  2005 "cs.ATG" 
			pexpr = expr; 
			break;
		}
		default: SynErr(180); break;
		}
		while (StartOf(25) || 
#line  2016 "cs.ATG" 
IsGenericFollowedBy(Tokens.Dot) && IsTypeReferenceExpression(pexpr) || 
#line  2024 "cs.ATG" 
IsGenericFollowedBy(Tokens.OpenParenthesis)) {
			if (la.kind == 30 || la.kind == 31) {
				if (la.kind == 30) {
					lexer.NextToken();

#line  2009 "cs.ATG" 
					pexpr = new UnaryOperatorExpression(pexpr, UnaryOperatorType.PostIncrement); 
				} else if (la.kind == 31) {
					lexer.NextToken();

#line  2010 "cs.ATG" 
					pexpr = new UnaryOperatorExpression(pexpr, UnaryOperatorType.PostDecrement); 
				} else SynErr(181);
			} else if (la.kind == 46) {
				lexer.NextToken();
				Expect(1);

#line  2013 "cs.ATG" 
				pexpr = new PointerReferenceExpression(pexpr, t.val); 
			} else if (la.kind == 14) {
				lexer.NextToken();
				Expect(1);

#line  2014 "cs.ATG" 
				pexpr = new FieldReferenceExpression(pexpr, t.val);
			} else if (
#line  2016 "cs.ATG" 
IsGenericFollowedBy(Tokens.Dot) && IsTypeReferenceExpression(pexpr)) {
				TypeArgumentList(
#line  2017 "cs.ATG" 
out typeList);
				Expect(14);
				Expect(1);

#line  2018 "cs.ATG" 
				pexpr = new FieldReferenceExpression(GetTypeReferenceExpression(pexpr, typeList), t.val);
			} else if (la.kind == 19) {
				lexer.NextToken();

#line  2020 "cs.ATG" 
				ArrayList parameters = new ArrayList(); 
				if (StartOf(21)) {
					Argument(
#line  2021 "cs.ATG" 
out expr);

#line  2021 "cs.ATG" 
					if (expr != null) {parameters.Add(expr);} 
					while (la.kind == 13) {
						lexer.NextToken();
						Argument(
#line  2022 "cs.ATG" 
out expr);

#line  2022 "cs.ATG" 
						if (expr != null) {parameters.Add(expr);} 
					}
				}
				Expect(20);

#line  2023 "cs.ATG" 
				pexpr = new InvocationExpression(pexpr, parameters); 
			} else if (
#line  2024 "cs.ATG" 
IsGenericFollowedBy(Tokens.OpenParenthesis)) {
				TypeArgumentList(
#line  2024 "cs.ATG" 
out typeList);
				Expect(19);

#line  2025 "cs.ATG" 
				ArrayList parameters = new ArrayList(); 
				if (StartOf(21)) {
					Argument(
#line  2026 "cs.ATG" 
out expr);

#line  2026 "cs.ATG" 
					if (expr != null) {parameters.Add(expr);} 
					while (la.kind == 13) {
						lexer.NextToken();
						Argument(
#line  2027 "cs.ATG" 
out expr);

#line  2027 "cs.ATG" 
						if (expr != null) {parameters.Add(expr);} 
					}
				}
				Expect(20);

#line  2028 "cs.ATG" 
				pexpr = new InvocationExpression(pexpr, parameters, typeList); 
			} else {

#line  2030 "cs.ATG" 
				if (isArrayCreation) Error("element access not allow on array creation");
				ArrayList indices = new ArrayList();
				
				lexer.NextToken();
				Expr(
#line  2033 "cs.ATG" 
out expr);

#line  2033 "cs.ATG" 
				if (expr != null) { indices.Add(expr); } 
				while (la.kind == 13) {
					lexer.NextToken();
					Expr(
#line  2034 "cs.ATG" 
out expr);

#line  2034 "cs.ATG" 
					if (expr != null) { indices.Add(expr); } 
				}
				Expect(18);

#line  2035 "cs.ATG" 
				pexpr = new IndexerExpression(pexpr, indices); 
			}
		}
	}

	void AnonymousMethodExpr(
#line  2039 "cs.ATG" 
out Expression outExpr) {

#line  2041 "cs.ATG" 
		AnonymousMethodExpression expr = new AnonymousMethodExpression();
		expr.StartLocation = t.Location;
		Statement stmt;
		ArrayList p;
		outExpr = expr;
		
		Expect(19);
		if (StartOf(9)) {
			FormalParameterList(
#line  2049 "cs.ATG" 
out p);

#line  2049 "cs.ATG" 
			expr.Parameters = p; 
		}
		Expect(20);

#line  2053 "cs.ATG" 
		if (compilationUnit != null) { 
		Block(
#line  2054 "cs.ATG" 
out stmt);

#line  2054 "cs.ATG" 
		expr.Body  = (BlockStatement)stmt; 

#line  2055 "cs.ATG" 
		} else { 
		Expect(15);

#line  2057 "cs.ATG" 
		lexer.SkipCurrentBlock(); 
		Expect(16);

#line  2059 "cs.ATG" 
		} 

#line  2060 "cs.ATG" 
		expr.EndLocation = t.Location; 
	}

	void TypeArgumentList(
#line  2192 "cs.ATG" 
out List<TypeReference> types) {

#line  2194 "cs.ATG" 
		types = new List<TypeReference>();
		TypeReference type = null;
		
		Expect(22);
		Type(
#line  2198 "cs.ATG" 
out type);

#line  2198 "cs.ATG" 
		types.Add(type); 
		while (la.kind == 13) {
			lexer.NextToken();
			Type(
#line  2199 "cs.ATG" 
out type);

#line  2199 "cs.ATG" 
			types.Add(type); 
		}
		Expect(21);
	}

	void ConditionalAndExpr(
#line  2069 "cs.ATG" 
ref Expression outExpr) {

#line  2070 "cs.ATG" 
		Expression expr; 
		InclusiveOrExpr(
#line  2072 "cs.ATG" 
ref outExpr);
		while (la.kind == 24) {
			lexer.NextToken();
			UnaryExpr(
#line  2072 "cs.ATG" 
out expr);
			InclusiveOrExpr(
#line  2072 "cs.ATG" 
ref expr);

#line  2072 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.LogicalAnd, expr);  
		}
	}

	void InclusiveOrExpr(
#line  2075 "cs.ATG" 
ref Expression outExpr) {

#line  2076 "cs.ATG" 
		Expression expr; 
		ExclusiveOrExpr(
#line  2078 "cs.ATG" 
ref outExpr);
		while (la.kind == 28) {
			lexer.NextToken();
			UnaryExpr(
#line  2078 "cs.ATG" 
out expr);
			ExclusiveOrExpr(
#line  2078 "cs.ATG" 
ref expr);

#line  2078 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.BitwiseOr, expr);  
		}
	}

	void ExclusiveOrExpr(
#line  2081 "cs.ATG" 
ref Expression outExpr) {

#line  2082 "cs.ATG" 
		Expression expr; 
		AndExpr(
#line  2084 "cs.ATG" 
ref outExpr);
		while (la.kind == 29) {
			lexer.NextToken();
			UnaryExpr(
#line  2084 "cs.ATG" 
out expr);
			AndExpr(
#line  2084 "cs.ATG" 
ref expr);

#line  2084 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.ExclusiveOr, expr);  
		}
	}

	void AndExpr(
#line  2087 "cs.ATG" 
ref Expression outExpr) {

#line  2088 "cs.ATG" 
		Expression expr; 
		EqualityExpr(
#line  2090 "cs.ATG" 
ref outExpr);
		while (la.kind == 27) {
			lexer.NextToken();
			UnaryExpr(
#line  2090 "cs.ATG" 
out expr);
			EqualityExpr(
#line  2090 "cs.ATG" 
ref expr);

#line  2090 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.BitwiseAnd, expr);  
		}
	}

	void EqualityExpr(
#line  2093 "cs.ATG" 
ref Expression outExpr) {

#line  2095 "cs.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		RelationalExpr(
#line  2099 "cs.ATG" 
ref outExpr);
		while (la.kind == 32 || la.kind == 33) {
			if (la.kind == 33) {
				lexer.NextToken();

#line  2102 "cs.ATG" 
				op = BinaryOperatorType.InEquality; 
			} else {
				lexer.NextToken();

#line  2103 "cs.ATG" 
				op = BinaryOperatorType.Equality; 
			}
			UnaryExpr(
#line  2105 "cs.ATG" 
out expr);
			RelationalExpr(
#line  2105 "cs.ATG" 
ref expr);

#line  2105 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void RelationalExpr(
#line  2109 "cs.ATG" 
ref Expression outExpr) {

#line  2111 "cs.ATG" 
		TypeReference type;
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		ShiftExpr(
#line  2116 "cs.ATG" 
ref outExpr);
		while (StartOf(26)) {
			if (StartOf(27)) {
				if (la.kind == 22) {
					lexer.NextToken();

#line  2119 "cs.ATG" 
					op = BinaryOperatorType.LessThan; 
				} else if (la.kind == 21) {
					lexer.NextToken();

#line  2120 "cs.ATG" 
					op = BinaryOperatorType.GreaterThan; 
				} else if (la.kind == 35) {
					lexer.NextToken();

#line  2121 "cs.ATG" 
					op = BinaryOperatorType.LessThanOrEqual; 
				} else if (la.kind == 34) {
					lexer.NextToken();

#line  2122 "cs.ATG" 
					op = BinaryOperatorType.GreaterThanOrEqual; 
				} else SynErr(182);
				UnaryExpr(
#line  2124 "cs.ATG" 
out expr);
				ShiftExpr(
#line  2124 "cs.ATG" 
ref expr);

#line  2124 "cs.ATG" 
				outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
			} else {
				if (la.kind == 83) {
					lexer.NextToken();

#line  2127 "cs.ATG" 
					op = BinaryOperatorType.IS; 
				} else if (la.kind == 48) {
					lexer.NextToken();

#line  2128 "cs.ATG" 
					op = BinaryOperatorType.AS; 
				} else SynErr(183);
				Type(
#line  2130 "cs.ATG" 
out type);

#line  2130 "cs.ATG" 
				outExpr = new BinaryOperatorExpression(outExpr, op, new TypeReferenceExpression(type)); 
			}
		}
	}

	void ShiftExpr(
#line  2134 "cs.ATG" 
ref Expression outExpr) {

#line  2136 "cs.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		AdditiveExpr(
#line  2140 "cs.ATG" 
ref outExpr);
		while (la.kind == 36 || 
#line  2143 "cs.ATG" 
IsShiftRight()) {
			if (la.kind == 36) {
				lexer.NextToken();

#line  2142 "cs.ATG" 
				op = BinaryOperatorType.ShiftLeft; 
			} else {
				Expect(21);
				Expect(21);

#line  2144 "cs.ATG" 
				op = BinaryOperatorType.ShiftRight; 
			}
			UnaryExpr(
#line  2147 "cs.ATG" 
out expr);
			AdditiveExpr(
#line  2147 "cs.ATG" 
ref expr);

#line  2147 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void AdditiveExpr(
#line  2151 "cs.ATG" 
ref Expression outExpr) {

#line  2153 "cs.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		MultiplicativeExpr(
#line  2157 "cs.ATG" 
ref outExpr);
		while (la.kind == 4 || la.kind == 5) {
			if (la.kind == 4) {
				lexer.NextToken();

#line  2160 "cs.ATG" 
				op = BinaryOperatorType.Add; 
			} else {
				lexer.NextToken();

#line  2161 "cs.ATG" 
				op = BinaryOperatorType.Subtract; 
			}
			UnaryExpr(
#line  2163 "cs.ATG" 
out expr);
			MultiplicativeExpr(
#line  2163 "cs.ATG" 
ref expr);

#line  2163 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void MultiplicativeExpr(
#line  2167 "cs.ATG" 
ref Expression outExpr) {

#line  2169 "cs.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		while (la.kind == 6 || la.kind == 7 || la.kind == 8) {
			if (la.kind == 6) {
				lexer.NextToken();

#line  2175 "cs.ATG" 
				op = BinaryOperatorType.Multiply; 
			} else if (la.kind == 7) {
				lexer.NextToken();

#line  2176 "cs.ATG" 
				op = BinaryOperatorType.Divide; 
			} else {
				lexer.NextToken();

#line  2177 "cs.ATG" 
				op = BinaryOperatorType.Modulus; 
			}
			UnaryExpr(
#line  2179 "cs.ATG" 
out expr);

#line  2179 "cs.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr); 
		}
	}

	void TypeParameterConstraintsClauseBase(
#line  2244 "cs.ATG" 
out TypeReference type) {

#line  2245 "cs.ATG" 
		TypeReference t; type = null; 
		if (la.kind == 107) {
			lexer.NextToken();

#line  2247 "cs.ATG" 
			type = new TypeReference("struct"); 
		} else if (la.kind == 57) {
			lexer.NextToken();

#line  2248 "cs.ATG" 
			type = new TypeReference("struct"); 
		} else if (la.kind == 87) {
			lexer.NextToken();
			Expect(19);
			Expect(20);

#line  2249 "cs.ATG" 
			type = new TypeReference("struct"); 
		} else if (StartOf(8)) {
			Type(
#line  2250 "cs.ATG" 
out t);

#line  2250 "cs.ATG" 
			type = t; 
		} else SynErr(184);
	}


	public Parser(ILexer lexer) : base(lexer)
	{
	}
	
	public override void Parse()
	{
		CS();

	}
	
	protected void ExpectWeak(int n, int follow)
	{
		if (lexer.LookAhead.kind == n) {
			lexer.NextToken();
		} else {
			SynErr(n);
			while (!StartOf(follow)) {
				lexer.NextToken();
			}
		}
	}
	
	protected bool WeakSeparator(int n, int syFol, int repFol)
	{
		bool[] s = new bool[maxT + 1];
		
		if (lexer.LookAhead.kind == n) {
			lexer.NextToken();
			return true;
		} else if (StartOf(repFol)) {
			return false;
		} else {
			for (int i = 0; i <= maxT; i++) {
				s[i] = set[syFol, i] || set[repFol, i] || set[0, i];
			}
			SynErr(n);
			while (!s[lexer.LookAhead.kind]) {
				lexer.NextToken();
			}
			return StartOf(syFol);
		}
	}
	
	protected override void SynErr(int line, int col, int errorNumber)
	{
		errors.count++; 
		string s;
		switch (errorNumber) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "Literal expected"; break;
			case 3: s = "\"=\" expected"; break;
			case 4: s = "\"+\" expected"; break;
			case 5: s = "\"-\" expected"; break;
			case 6: s = "\"*\" expected"; break;
			case 7: s = "\"/\" expected"; break;
			case 8: s = "\"%\" expected"; break;
			case 9: s = "\":\" expected"; break;
			case 10: s = "\"::\" expected"; break;
			case 11: s = "\";\" expected"; break;
			case 12: s = "\"?\" expected"; break;
			case 13: s = "\",\" expected"; break;
			case 14: s = "\".\" expected"; break;
			case 15: s = "\"{\" expected"; break;
			case 16: s = "\"}\" expected"; break;
			case 17: s = "\"[\" expected"; break;
			case 18: s = "\"]\" expected"; break;
			case 19: s = "\"(\" expected"; break;
			case 20: s = "\")\" expected"; break;
			case 21: s = "\">\" expected"; break;
			case 22: s = "\"<\" expected"; break;
			case 23: s = "\"!\" expected"; break;
			case 24: s = "\"&&\" expected"; break;
			case 25: s = "\"||\" expected"; break;
			case 26: s = "\"~\" expected"; break;
			case 27: s = "\"&\" expected"; break;
			case 28: s = "\"|\" expected"; break;
			case 29: s = "\"^\" expected"; break;
			case 30: s = "\"++\" expected"; break;
			case 31: s = "\"--\" expected"; break;
			case 32: s = "\"==\" expected"; break;
			case 33: s = "\"!=\" expected"; break;
			case 34: s = "\">=\" expected"; break;
			case 35: s = "\"<=\" expected"; break;
			case 36: s = "\"<<\" expected"; break;
			case 37: s = "\"+=\" expected"; break;
			case 38: s = "\"-=\" expected"; break;
			case 39: s = "\"*=\" expected"; break;
			case 40: s = "\"/=\" expected"; break;
			case 41: s = "\"%=\" expected"; break;
			case 42: s = "\"&=\" expected"; break;
			case 43: s = "\"|=\" expected"; break;
			case 44: s = "\"^=\" expected"; break;
			case 45: s = "\"<<=\" expected"; break;
			case 46: s = "\"->\" expected"; break;
			case 47: s = "\"abstract\" expected"; break;
			case 48: s = "\"as\" expected"; break;
			case 49: s = "\"base\" expected"; break;
			case 50: s = "\"bool\" expected"; break;
			case 51: s = "\"break\" expected"; break;
			case 52: s = "\"byte\" expected"; break;
			case 53: s = "\"case\" expected"; break;
			case 54: s = "\"catch\" expected"; break;
			case 55: s = "\"char\" expected"; break;
			case 56: s = "\"checked\" expected"; break;
			case 57: s = "\"class\" expected"; break;
			case 58: s = "\"const\" expected"; break;
			case 59: s = "\"continue\" expected"; break;
			case 60: s = "\"decimal\" expected"; break;
			case 61: s = "\"default\" expected"; break;
			case 62: s = "\"delegate\" expected"; break;
			case 63: s = "\"do\" expected"; break;
			case 64: s = "\"double\" expected"; break;
			case 65: s = "\"else\" expected"; break;
			case 66: s = "\"enum\" expected"; break;
			case 67: s = "\"event\" expected"; break;
			case 68: s = "\"explicit\" expected"; break;
			case 69: s = "\"extern\" expected"; break;
			case 70: s = "\"false\" expected"; break;
			case 71: s = "\"finally\" expected"; break;
			case 72: s = "\"fixed\" expected"; break;
			case 73: s = "\"float\" expected"; break;
			case 74: s = "\"for\" expected"; break;
			case 75: s = "\"foreach\" expected"; break;
			case 76: s = "\"goto\" expected"; break;
			case 77: s = "\"if\" expected"; break;
			case 78: s = "\"implicit\" expected"; break;
			case 79: s = "\"in\" expected"; break;
			case 80: s = "\"int\" expected"; break;
			case 81: s = "\"interface\" expected"; break;
			case 82: s = "\"internal\" expected"; break;
			case 83: s = "\"is\" expected"; break;
			case 84: s = "\"lock\" expected"; break;
			case 85: s = "\"long\" expected"; break;
			case 86: s = "\"namespace\" expected"; break;
			case 87: s = "\"new\" expected"; break;
			case 88: s = "\"null\" expected"; break;
			case 89: s = "\"object\" expected"; break;
			case 90: s = "\"operator\" expected"; break;
			case 91: s = "\"out\" expected"; break;
			case 92: s = "\"override\" expected"; break;
			case 93: s = "\"params\" expected"; break;
			case 94: s = "\"private\" expected"; break;
			case 95: s = "\"protected\" expected"; break;
			case 96: s = "\"public\" expected"; break;
			case 97: s = "\"readonly\" expected"; break;
			case 98: s = "\"ref\" expected"; break;
			case 99: s = "\"return\" expected"; break;
			case 100: s = "\"sbyte\" expected"; break;
			case 101: s = "\"sealed\" expected"; break;
			case 102: s = "\"short\" expected"; break;
			case 103: s = "\"sizeof\" expected"; break;
			case 104: s = "\"stackalloc\" expected"; break;
			case 105: s = "\"static\" expected"; break;
			case 106: s = "\"string\" expected"; break;
			case 107: s = "\"struct\" expected"; break;
			case 108: s = "\"switch\" expected"; break;
			case 109: s = "\"this\" expected"; break;
			case 110: s = "\"throw\" expected"; break;
			case 111: s = "\"true\" expected"; break;
			case 112: s = "\"try\" expected"; break;
			case 113: s = "\"typeof\" expected"; break;
			case 114: s = "\"uint\" expected"; break;
			case 115: s = "\"ulong\" expected"; break;
			case 116: s = "\"unchecked\" expected"; break;
			case 117: s = "\"unsafe\" expected"; break;
			case 118: s = "\"ushort\" expected"; break;
			case 119: s = "\"using\" expected"; break;
			case 120: s = "\"virtual\" expected"; break;
			case 121: s = "\"void\" expected"; break;
			case 122: s = "\"volatile\" expected"; break;
			case 123: s = "\"while\" expected"; break;
			case 124: s = "??? expected"; break;
			case 125: s = "invalid NamespaceMemberDecl"; break;
			case 126: s = "invalid AttributeArguments"; break;
			case 127: s = "invalid Expr"; break;
			case 128: s = "invalid TypeModifier"; break;
			case 129: s = "invalid TypeDecl"; break;
			case 130: s = "invalid TypeDecl"; break;
			case 131: s = "invalid IntegralType"; break;
			case 132: s = "invalid Type"; break;
			case 133: s = "invalid Type"; break;
			case 134: s = "invalid FormalParameterList"; break;
			case 135: s = "invalid FormalParameterList"; break;
			case 136: s = "invalid ClassType"; break;
			case 137: s = "invalid MemberModifier"; break;
			case 138: s = "invalid ClassMemberDecl"; break;
			case 139: s = "invalid ClassMemberDecl"; break;
			case 140: s = "invalid StructMemberDecl"; break;
			case 141: s = "invalid StructMemberDecl"; break;
			case 142: s = "invalid StructMemberDecl"; break;
			case 143: s = "invalid StructMemberDecl"; break;
			case 144: s = "invalid StructMemberDecl"; break;
			case 145: s = "invalid StructMemberDecl"; break;
			case 146: s = "invalid StructMemberDecl"; break;
			case 147: s = "invalid StructMemberDecl"; break;
			case 148: s = "invalid StructMemberDecl"; break;
			case 149: s = "invalid StructMemberDecl"; break;
			case 150: s = "invalid InterfaceMemberDecl"; break;
			case 151: s = "invalid InterfaceMemberDecl"; break;
			case 152: s = "invalid InterfaceMemberDecl"; break;
			case 153: s = "invalid SimpleType"; break;
			case 154: s = "invalid NonArrayType"; break;
			case 155: s = "invalid EventAccessorDecls"; break;
			case 156: s = "invalid ConstructorInitializer"; break;
			case 157: s = "invalid OverloadableOperator"; break;
			case 158: s = "invalid AccessorDecls"; break;
			case 159: s = "invalid InterfaceAccessors"; break;
			case 160: s = "invalid InterfaceAccessors"; break;
			case 161: s = "invalid GetAccessorDecl"; break;
			case 162: s = "invalid SetAccessorDecl"; break;
			case 163: s = "invalid VariableInitializer"; break;
			case 164: s = "invalid Statement"; break;
			case 165: s = "invalid AssignmentOperator"; break;
			case 166: s = "invalid EmbeddedStatement"; break;
			case 167: s = "invalid EmbeddedStatement"; break;
			case 168: s = "invalid EmbeddedStatement"; break;
			case 169: s = "invalid ForInitializer"; break;
			case 170: s = "invalid GotoStatement"; break;
			case 171: s = "invalid StatementExpr"; break;
			case 172: s = "invalid TryStatement"; break;
			case 173: s = "invalid ResourceAcquisition"; break;
			case 174: s = "invalid SwitchLabel"; break;
			case 175: s = "invalid CatchClauses"; break;
			case 176: s = "invalid PrimaryExpr"; break;
			case 177: s = "invalid PrimaryExpr"; break;
			case 178: s = "invalid PrimaryExpr"; break;
			case 179: s = "invalid PrimaryExpr"; break;
			case 180: s = "invalid PrimaryExpr"; break;
			case 181: s = "invalid PrimaryExpr"; break;
			case 182: s = "invalid RelationalExpr"; break;
			case 183: s = "invalid RelationalExpr"; break;
			case 184: s = "invalid TypeParameterConstraintsClauseBase"; break;

			default: s = "error " + errorNumber; break;
		}
		errors.Error(line, col, s);
	}
	
	protected bool StartOf(int s)
	{
		return set[s, lexer.LookAhead.kind];
	}
	
	static bool[,] set = {
	{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,T,x,x, x,x,T,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, x,x,T,T, x,x,x,x, x,x,T,T, T,x,x,x, x,T,x,x, x,T,x,T, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,T,x,x, x,x,T,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, x,x,x,T, x,x,x,x, x,x,T,T, T,x,x,x, x,T,x,x, x,T,x,T, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,T, x,x,x,x, x,x,T,T, T,x,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x},
	{x,T,T,x, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, T,x,x,T, T,x,x,x, T,x,T,x, T,x,x,x, x,x,T,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,T, T,T,x,x, x,x,x,x, x,x,x,x, T,x,T,T, x,x,T,x, x,T,x,T, x,T,T,T, T,x,T,x, x,x,x,x, x,x},
	{x,x,x,x, T,T,T,T, T,T,x,T, T,T,x,x, T,x,T,x, T,T,T,x, T,T,x,T, T,T,x,x, T,T,T,T, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,T,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,T,x, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,T,x,T, x,T,x,x, x,x,T,x, T,x,T,x, x,x,T,x, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,x, T,x,x,T, x,T,T,x, T,x,T,x, T,x,T,T, T,T,x,x, x,T,x,x, x,x,T,x, T,T,T,x, x,T,x,T, x,T,x,x, T,x,T,T, T,T,x,x, T,T,T,x, x,T,T,T, x,x,x,x, x,x,T,T, x,T,T,x, T,T,T,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,T, x,x,x,x, T,x,T,T, T,T,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,T,x,x, T,x,T,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,x, T,x,x,T, x,T,T,x, T,x,T,x, T,x,T,T, T,T,x,x, x,T,x,x, x,x,T,x, T,T,T,x, x,T,x,T, x,T,x,x, T,x,T,T, T,T,x,x, T,T,T,x, x,T,T,T, x,x,x,x, x,x,T,T, x,T,T,x, T,T,T,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,T, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,T, x,T,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,T,x, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,T,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,T,x,T, x,x,x,x, x,x,T,x, T,x,T,x, x,x,T,x, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,T,T,x, T,x,T,x, T,x,T,T, T,x,x,x, x,T,x,x, x,x,T,x, T,T,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,T,T, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,T,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,T, x,x,x,x, T,x,x,x, T,x,x,T, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,T,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,T,x, x,x,x,x, x,x,T,T, x,x,T,x, x,T,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,T,x, x,x,x,x, x,x},
	{x,T,T,x, T,T,T,x, x,x,x,T, x,x,x,T, x,x,x,T, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,x,x,T, T,x,T,T, T,x,T,T, T,x,x,x, x,x,T,x, T,T,T,T, T,T,x,x, T,x,x,x, T,T,x,T, T,T,x,x, x,x,x,x, x,x,x,T, T,x,T,T, x,x,T,x, T,T,T,T, T,T,T,T, T,T,T,T, x,x,x,T, x,x},
	{x,T,T,x, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, T,x,x,T, T,x,x,x, T,x,T,x, T,x,x,x, x,x,T,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,T, T,T,x,T, x,x,x,x, x,x,T,x, T,x,T,T, x,x,T,x, x,T,x,T, x,T,T,T, T,x,T,x, x,x,x,x, x,x},
	{x,T,T,x, T,T,T,x, x,x,x,T, x,x,x,T, x,x,x,T, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,x,x,T, T,x,x,T, T,x,T,T, T,x,x,x, x,x,T,x, T,T,T,T, T,T,x,x, T,x,x,x, T,T,x,T, T,T,x,x, x,x,x,x, x,x,x,T, T,x,T,T, x,x,T,x, T,T,T,T, T,T,T,T, T,T,T,T, x,x,x,T, x,x},
	{x,T,T,x, T,T,T,x, x,x,x,x, x,x,x,T, x,x,x,T, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, T,x,x,T, T,x,x,x, T,x,T,x, T,x,x,x, x,x,T,x, x,T,x,x, x,x,x,x, T,x,x,x, x,T,x,T, T,T,x,x, x,x,x,x, x,x,x,x, T,x,T,T, T,x,T,x, x,T,x,T, x,T,T,T, T,x,T,x, x,x,x,x, x,x},
	{x,x,x,x, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,T, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,T,x,T, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x}

	};
} // end Parser

}