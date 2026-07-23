from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List
import sympy

# The correct import path for the parsing tools
from sympy.parsing.sympy_parser import (
    parse_expr, 
    standard_transformations, 
    implicit_multiplication_application, 
    convert_xor
)

app = FastAPI(title="Graphle Math Engine")

@app.get("/")
def read_root():
    return {"status": "Math Engine Online"}

# --- Data Models (Pydantic) ---
class Point(BaseModel):
    x: float
    y: float

class ValidationRequest(BaseModel):
    equation: str
    targets: List[Point]

# Match the tolerance from our frontend canvas
HIT_TOLERANCE = 0.3

# --- Validation Endpoint ---
@app.post("/validate")
def validate_equation(request: ValidationRequest):
    try:
        # standard_transformations handles basic math
        # implicit_multiplication handles "2x" -> "2*x"
        # convert_xor handles "^" -> "**"
        transformations = (standard_transformations + (implicit_multiplication_application, convert_xor))
        
        # Pre-clean caret ^ to ** for reliable parsing
        clean_equation = request.equation.replace('^', '**')

        # Parse the string into a secure mathematical expression
        x_sym = sympy.Symbol('x')
        expr = parse_expr(clean_equation, transformations=transformations, evaluate=True)

        # Verify against each target point
        for target in request.targets:
            # Evaluate the user's equation at the target's X coordinate
            calculated_y = float(expr.subs(x_sym, target.x).evalf())
            
            # Check if the curve passes within the tolerance hit box
            if abs(calculated_y - target.y) > HIT_TOLERANCE:
                return {"isValid": False, "message": f"Missed target at x={target.x}"}

        # If loop finishes without returning False, they hit every point!
        return {"isValid": True, "message": "All targets hit!"}

    except Exception as e:
        # Catch invalid math syntax and report explicit detail
        print(f"Validation error: {e}")
        raise HTTPException(status_code=400, detail=f"Invalid expression: {str(e)}")