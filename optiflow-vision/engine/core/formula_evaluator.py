"""
OptiFlow Vision Studio - Safe Formula & Expression Evaluator
Sandboxed AST-based mathematical and logic expression parser.
"""

import ast
import math
import operator
from typing import Any, Dict, Optional


class SafeFormulaSecurityError(Exception):
    """Raised when an expression attempts to execute forbidden functions or syntax."""
    pass


class SafeExpressionEvaluator:
    SAFE_OPERATORS = {
        ast.Add: operator.add,
        ast.Sub: operator.sub,
        ast.Mult: operator.mul,
        ast.Div: operator.truediv,
        ast.FloorDiv: operator.floordiv,
        ast.Mod: operator.mod,
        ast.Pow: operator.pow,
        ast.USub: operator.neg,
        ast.UAdd: operator.pos,
        ast.Not: operator.not_,
        ast.Eq: operator.eq,
        ast.NotEq: operator.ne,
        ast.Lt: operator.lt,
        ast.LtE: operator.le,
        ast.Gt: operator.gt,
        ast.GtE: operator.ge,
    }

    SAFE_FUNCTIONS = {
        "abs": abs,
        "min": min,
        "max": max,
        "round": round,
        "floor": math.floor,
        "ceil": math.ceil,
        "sqrt": math.sqrt,
        "hypot": math.hypot,
        "sin": math.sin,
        "cos": math.cos,
        "tan": math.tan,
        "atan2": math.atan2,
        "degrees": math.degrees,
        "radians": math.radians,
        "exp": math.exp,
        "log": math.log,
        "log10": math.log10,
        "pow": pow,
        "int": int,
        "float": float,
        "bool": bool,
        "str": str,
        "len": len,
    }

    SAFE_CONSTANTS = {
        "pi": math.pi,
        "e": math.e,
        "True": True,
        "False": False,
        "None": None,
        "Nil": None,
    }

    def evaluate(self, expression: str, context: Optional[Dict[str, Any]] = None) -> Any:
        if not expression or not expression.strip():
            return None
        
        ctx = context or {}
        tree = ast.parse(expression.strip(), mode="eval")
        return self._eval_node(tree.body, ctx)

    def _eval_node(self, node: ast.AST, context: Dict[str, Any]) -> Any:
        if isinstance(node, ast.Constant):
            return node.value

        elif isinstance(node, ast.Name):
            if node.id in context:
                return context[node.id]
            if node.id in self.SAFE_CONSTANTS:
                return self.SAFE_CONSTANTS[node.id]
            if node.id in self.SAFE_FUNCTIONS:
                return self.SAFE_FUNCTIONS[node.id]
            raise SafeFormulaSecurityError(f"Access to unauthorized identifier or function: '{node.id}'")

        elif isinstance(node, ast.UnaryOp):
            op_type = type(node.op)
            if op_type in self.SAFE_OPERATORS:
                operand = self._eval_node(node.operand, context)
                return self.SAFE_OPERATORS[op_type](operand)
            raise SafeFormulaSecurityError(f"Unsupported unary operator: {op_type.__name__}")

        elif isinstance(node, ast.BinOp):
            op_type = type(node.op)
            if op_type in self.SAFE_OPERATORS:
                left = self._eval_node(node.left, context)
                right = self._eval_node(node.right, context)
                return self.SAFE_OPERATORS[op_type](left, right)
            raise SafeFormulaSecurityError(f"Unsupported binary operator: {op_type.__name__}")

        elif isinstance(node, ast.Compare):
            left = self._eval_node(node.left, context)
            for op, comparator in zip(node.ops, node.comparators):
                op_type = type(op)
                if op_type in self.SAFE_OPERATORS:
                    right = self._eval_node(comparator, context)
                    if not self.SAFE_OPERATORS[op_type](left, right):
                        return False
                    left = right
                else:
                    raise SafeFormulaSecurityError(f"Unsupported comparison operator: {op_type.__name__}")
            return True

        elif isinstance(node, ast.BoolOp):
            if isinstance(node.op, ast.And):
                for val in node.values:
                    if not self._eval_node(val, context):
                        return False
                return True
            elif isinstance(node.op, ast.Or):
                for val in node.values:
                    res = self._eval_node(val, context)
                    if res:
                        return res
                return False
            raise SafeFormulaSecurityError(f"Unsupported boolean operator: {type(node.op).__name__}")

        elif isinstance(node, ast.IfExp):
            cond = self._eval_node(node.test, context)
            if cond:
                return self._eval_node(node.body, context)
            else:
                return self._eval_node(node.orelse, context)

        elif isinstance(node, ast.Call):
            func = self._eval_node(node.func, context)
            if not callable(func):
                raise SafeFormulaSecurityError("Calling uncallable target")
            if func not in self.SAFE_FUNCTIONS.values() and not (callable(func) and any(func is v for v in context.values())):
                raise SafeFormulaSecurityError("Calling unauthorized function")
            args = [self._eval_node(arg, context) for arg in node.args]
            kwargs = {kw.arg: self._eval_node(kw.value, context) for kw in node.keywords if kw.arg}
            return func(*args, **kwargs)

        elif isinstance(node, ast.List):
            return [self._eval_node(el, context) for el in node.elts]

        elif isinstance(node, ast.Dict):
            return {
                self._eval_node(k, context): self._eval_node(v, context)
                for k, v in zip(node.keys, node.values)
                if k is not None
            }

        elif isinstance(node, ast.Subscript):
            val = self._eval_node(node.value, context)
            idx = self._eval_node(node.slice, context)
            return val[idx]

        raise SafeFormulaSecurityError(f"Unsupported or forbidden expression syntax: {type(node).__name__}")


formula_evaluator = SafeExpressionEvaluator()


def safe_evaluate_formula(expression: str, context: Optional[Dict[str, Any]] = None) -> Any:
    """Helper function to safely evaluate a mathematical or logic formula expression."""
    return formula_evaluator.evaluate(expression, context)
