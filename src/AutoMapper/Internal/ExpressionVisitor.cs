using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
//using System.Dynamic.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastExpressionCompiler.LightExpression
{
    public abstract class ExpressionVisitor
    {
        public virtual Expression Visit(Expression node) => node?.Accept(this);

        public IReadOnlyList<Expression> Visit(IReadOnlyList<Expression> nodes)
        {
            Expression[] expressionArray = null;
            var count = nodes.Count;
            for (var i = 0; i < count; ++i)
            {
                var expression = Visit(nodes[i]);
                if (expressionArray != null)
                {
                    expressionArray[i] = expression;
                }
                else if (expression != nodes[i])
                {
                    expressionArray = new Expression[count];
                    for (var j = 0; j < i; ++j)
                        expressionArray[j] = nodes[j];
                    expressionArray[i] = expression;
                }
            }
            if (expressionArray == null)
                return nodes;
            return expressionArray;
        }

        public static Expression[] VisitBlockExpressions(ExpressionVisitor visitor, BlockExpression block) => 
            VisitMany(visitor, block.Expressions);

        public static ParameterExpression[] VisitParameters(
            ExpressionVisitor visitor, IReadOnlyList<ParameterExpression> parameterExpressions, string callerName)
        {
            ParameterExpression[] newExpressions = null;
            
            var count = parameterExpressions.Count;
            for (var i = 0; i < count; ++i)
            {
                var oldExpr = parameterExpressions[i];
                var newExpr = visitor.VisitAndConvert(oldExpr, callerName);
                if (newExpressions != null)
                    newExpressions[i] = newExpr;
                else if (newExpr != oldExpr)
                {
                    newExpressions = new ParameterExpression[count];
                    for (var j = 0; j < i; ++j)
                        newExpressions[j] = parameterExpressions[j];
                    newExpressions[i] = newExpr;
                }
            }
            return newExpressions;
        }

        public static Expression[] VisitMany(ExpressionVisitor visitor, IReadOnlyList<Expression> expressions)
        {
            Expression[] newExpressions = null;
            var count = expressions.Count;
            for (var i = 0; i < count; ++i)
            {
                var oldExpr = expressions[i];
                var newExpr = visitor.Visit(oldExpr);
                if (newExpressions != null)
                    newExpressions[i] = newExpr;
                else if (newExpr != oldExpr)
                {
                    newExpressions = new Expression[count];
                    for (var j = 0; j < i; ++j)
                        newExpressions[j] = expressions[j];
                    newExpressions[i] = newExpr;
                }
            }
            return newExpressions;
        }

        public static IReadOnlyList<T> Visit<T>(IReadOnlyList<T> nodes, Func<T, T> elementVisitor)
        {
            var objArray = (T[])null;
            var index1 = 0;
            for (var count = nodes.Count; index1 < count; ++index1)
            {
                var obj = elementVisitor(nodes[index1]);
                if (objArray != null)
                    objArray[index1] = obj;
                else if ((object)obj != (object)nodes[index1])
                {
                    objArray = new T[count];
                    for (var index2 = 0; index2 < index1; ++index2)
                        objArray[index2] = nodes[index2];
                    objArray[index1] = obj;
                }
            }
            if (objArray == null)
                return nodes;
            return objArray;
        }

        public T VisitAndConvert<T>(T node, string callerName) where T : Expression
        {
            if (node == null)
                return default;
            var x = Visit(node);
            if (x is T converted)
                return converted;
            throw new InvalidOperationException($"Convert in the not compatible type {callerName} from {x?.GetType()} to {typeof(T)}");
        }

        public IReadOnlyList<T> VisitAndConvert<T>(IReadOnlyList<T> nodes, string callerName) where T : Expression
        {
            var objArray = (T[])null;
            var index1 = 0;
            for (var count = nodes.Count; index1 < count; ++index1)
            {
                var obj = this.Visit(nodes[index1]) as T;
                if (obj == null)
                    throw Error.MustRewriteToSameNode((object)callerName, (object)typeof(T), (object)callerName);
                if (objArray != null)
                    objArray[index1] = obj;
                else if (obj != nodes[index1])
                {
                    objArray = new T[count];
                    for (var index2 = 0; index2 < index1; ++index2)
                        objArray[index2] = nodes[index2];
                    objArray[index1] = obj;
                }
            }
            if (objArray == null)
                return nodes;
            return objArray;
        }

        protected internal virtual Expression VisitBinary(BinaryExpression node)
        {
            return ValidateBinary(node, node.Update(this.Visit(node.Left), this.VisitAndConvert<LambdaExpression>(node.Conversion, nameof(VisitBinary)), this.Visit(node.Right)));
        }

        protected internal virtual Expression VisitBlock(BlockExpression node)
        {
            var args = VisitBlockExpressions(this, node);
            var variables = VisitAndConvert(node.Variables, nameof(VisitBlock));
            if (ReferenceEquals(variables, node.Variables) && args == null)
                return node;
            return (Expression)node.Rewrite(variables, args);
        }

        protected internal virtual Expression VisitConditional(ConditionalExpression node)
        {
            return (Expression)node.Update(this.Visit(node.Test), this.Visit(node.IfTrue), this.Visit(node.IfFalse));
        }

        protected internal virtual Expression VisitConstant(ConstantExpression node)
        {
            return node;
        }

        protected internal virtual Expression VisitDebugInfo(DebugInfoExpression node)
        {
            return (Expression)node;
        }

        protected internal virtual Expression VisitDefault(DefaultExpression node)
        {
            return node;
        }

        protected internal virtual Expression VisitExtension(Expression node)
        {
            return node.VisitChildren(this);
        }

        protected internal virtual Expression VisitGoto(GotoExpression node)
        {
            return (Expression)node.Update(this.VisitLabelTarget(node.Target), this.Visit(node.Value));
        }

        protected internal virtual Expression VisitInvocation(InvocationExpression node)
        {
            var lambda = this.Visit(node.Expression);
            var arguments = this.VisitArguments((IArgumentProvider)node);
            if (lambda == node.Expression && arguments == null)
                return node;
            return (Expression)node.Rewrite(lambda, arguments);
        }

        protected virtual LabelTarget VisitLabelTarget(LabelTarget node)
        {
            return node;
        }

        protected internal virtual Expression VisitLabel(LabelExpression node)
        {
            return (Expression)node.Update(this.VisitLabelTarget(node.Target), this.Visit(node.DefaultValue));
        }

        protected internal virtual Expression VisitLambda<T>(Expression<T> node)
        {
            var body = this.Visit(node.Body);
            var parameters = this.VisitParameters((IParameterProvider)node, nameof(VisitLambda));
            if (body == node.Body && parameters == null)
                return node;
            return (Expression)node.Rewrite(body, parameters);
        }

        protected internal virtual Expression VisitLoop(LoopExpression node)
        {
            return (Expression)node.Update(this.VisitLabelTarget(node.BreakLabel), this.VisitLabelTarget(node.ContinueLabel), this.Visit(node.Body));
        }

        protected internal virtual Expression VisitMember(MemberExpression node)
        {
            return (Expression)node.Update(this.Visit(node.Expression));
        }

        protected internal virtual Expression VisitIndex(IndexExpression node)
        {
            var instance = this.Visit(node.Object);
            var arguments = this.VisitArguments((IArgumentProvider)node);
            if (instance == node.Object && arguments == null)
                return node;
            return node.Rewrite(instance, arguments);
        }

        protected internal virtual Expression VisitMethodCall(MethodCallExpression node)
        {
            var instance = this.Visit(node.Object);
            var expressionArray = this.VisitArguments((IArgumentProvider)node);
            if (instance == node.Object && expressionArray == null)
                return node;
            return (Expression)node.Rewrite(instance, (IReadOnlyList<Expression>)expressionArray);
        }

        protected internal virtual Expression VisitNewArray(NewArrayExpression node)
        {
            return (Expression)node.Update((IEnumerable<Expression>)this.Visit(node.Expressions));
        }

        protected internal virtual Expression VisitNew(NewExpression node)
        {
            var expressionArray = this.VisitArguments((IArgumentProvider)node);
            if (expressionArray == null)
                return node;
            return (Expression)node.Update((IEnumerable<Expression>)expressionArray);
        }

        protected internal virtual Expression VisitParameter(ParameterExpression node)
        {
            return node;
        }

        protected internal virtual Expression VisitRuntimeVariables(
          RuntimeVariablesExpression node)
        {
            return (Expression)node.Update((IEnumerable<ParameterExpression>)this.VisitAndConvert<ParameterExpression>(node.Variables, nameof(VisitRuntimeVariables)));
        }

        protected virtual SwitchCase VisitSwitchCase(SwitchCase node)
        {
            return node.Update((IEnumerable<Expression>)this.Visit(node.TestValues), this.Visit(node.Body));
        }

        protected internal virtual Expression VisitSwitch(SwitchExpression node)
        {
            return ExpressionVisitor.ValidateSwitch(node, node.Update(this.Visit(node.SwitchValue), (IEnumerable<SwitchCase>)ExpressionVisitor.Visit<SwitchCase>(node.Cases, new Func<SwitchCase, SwitchCase>(this.VisitSwitchCase)), this.Visit(node.DefaultBody)));
        }

        protected virtual CatchBlock VisitCatchBlock(CatchBlock node)
        {
            return node.Update(this.VisitAndConvert<ParameterExpression>(node.Variable, nameof(VisitCatchBlock)), this.Visit(node.Filter), this.Visit(node.Body));
        }

        protected internal virtual Expression VisitTry(TryExpression node)
        {
            return (Expression)node.Update(this.Visit(node.Body), (IEnumerable<CatchBlock>)ExpressionVisitor.Visit<CatchBlock>(node.Handlers, new Func<CatchBlock, CatchBlock>(this.VisitCatchBlock)), this.Visit(node.Finally), this.Visit(node.Fault));
        }

        protected internal virtual Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            return (Expression)node.Update(this.Visit(node.Expression));
        }

        protected internal virtual Expression VisitUnary(UnaryExpression node)
        {
            return ExpressionVisitor.ValidateUnary(node, node.Update(this.Visit(node.Operand)));
        }

        protected internal virtual Expression VisitMemberInit(MemberInitExpression node)
        {
            return (Expression)node.Update(this.VisitAndConvert<NewExpression>(node.NewExpression, nameof(VisitMemberInit)), (IEnumerable<MemberBinding>)ExpressionVisitor.Visit<MemberBinding>(node.Bindings, new Func<MemberBinding, MemberBinding>(this.VisitMemberBinding)));
        }

        protected internal virtual Expression VisitListInit(ListInitExpression node)
        {
            return (Expression)node.Update(this.VisitAndConvert<NewExpression>(node.NewExpression, nameof(VisitListInit)), (IEnumerable<ElementInit>)ExpressionVisitor.Visit<ElementInit>(node.Initializers, new Func<ElementInit, ElementInit>(this.VisitElementInit)));
        }

        protected virtual ElementInit VisitElementInit(ElementInit node)
        {
            return node.Update((IEnumerable<Expression>)this.Visit(node.Arguments));
        }

        protected virtual MemberBinding VisitMemberBinding(MemberBinding node)
        {
            switch (node.BindingType)
            {
                case MemberBindingType.Assignment:
                    return this.VisitMemberAssignment((MemberAssignment)node);
                case MemberBindingType.MemberBinding:
                    return (MemberBinding)this.VisitMemberMemberBinding((MemberMemberBinding)node);
                case MemberBindingType.ListBinding:
                    return (MemberBinding)this.VisitMemberListBinding((MemberListBinding)node);
                default:
                    throw Error.UnhandledBindingType((object)node.BindingType);
            }
        }

        protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            return node.Update(this.Visit(node.Expression));
        }

        protected virtual MemberMemberBinding VisitMemberMemberBinding(
          MemberMemberBinding node)
        {
            return node.Update((IEnumerable<MemberBinding>)ExpressionVisitor.Visit<MemberBinding>(node.Bindings, new Func<MemberBinding, MemberBinding>(this.VisitMemberBinding)));
        }

        protected virtual MemberListBinding VisitMemberListBinding(
          MemberListBinding node)
        {
            return node.Update((IEnumerable<ElementInit>)ExpressionVisitor.Visit<ElementInit>(node.Initializers, new Func<ElementInit, ElementInit>(this.VisitElementInit)));
        }

        private static UnaryExpression ValidateUnary(
          UnaryExpression before,
          UnaryExpression after)
        {
            if (before != after && before.Method == null)
            {
                if (after.Method != null)
                    throw Error.MustRewriteWithoutMethod((object)after.Method, (object)"VisitUnary");
                if (before.Operand != null && after.Operand != null)
                    ExpressionVisitor.ValidateChildType(before.Operand.Type, after.Operand.Type, "VisitUnary");
            }
            return after;
        }

        private static BinaryExpression ValidateBinary(
          BinaryExpression before,
          BinaryExpression after)
        {
            if (before != after && before.Method == (MethodInfo)null)
            {
                if (after.Method != (MethodInfo)null)
                    throw Error.MustRewriteWithoutMethod((object)after.Method, (object)"VisitBinary");
                ExpressionVisitor.ValidateChildType(before.Left.Type, after.Left.Type, "VisitBinary");
                ExpressionVisitor.ValidateChildType(before.Right.Type, after.Right.Type, "VisitBinary");
            }
            return after;
        }

        private static SwitchExpression ValidateSwitch(
          SwitchExpression before,
          SwitchExpression after)
        {
            if (before.Comparison == null && after.Comparison != null)
                throw Error.MustRewriteWithoutMethod((object)after.Comparison, (object)"VisitSwitch");
            return after;
        }

        private static void ValidateChildType(Type before, Type after, string methodName)
        {
            if (before.IsValueType)
            {
                if (TypeUtils.AreEquivalent(before, after))
                    return;
            }
            else if (!after.IsValueType)
                return;
            throw Error.MustRewriteChildToSameType((object)before, (object)after, (object)methodName);
        }

        protected internal virtual Expression VisitDynamic(DynamicExpression node)
        {
            var args = VisitMany(this, node.Arguments);
            if (args == null)
                return (Expression)node;
            return (Expression)node.Rewrite(args);
        }
    }
}
