import json
import random
import time
import os
import psycopg2
from datetime import date, timedelta

# Types of math functions our generator can build
def generate_parabola(x):
    a = random.choice([-1, -0.5, 0.5, 1, 2])
    c = random.randint(-5, 5)
    return round((a * (x**2)) + c, 2)

def generate_line(x):
    m = random.choice([-2, -1, 0.5, 1, 2])
    b = random.randint(-3, 3)
    return round((m * x) + b, 2)

def generate_absolute(x):
    a = random.choice([-2, -1, 1, 2])
    c = random.randint(-4, 4)
    return round((a * abs(x)) + c, 2)

def generate_and_insert_puzzle():
    print("Generating puzzle for tomorrow...")
    # Generate puzzle for TOMORROW
    puzzle_date = date.today() + timedelta(days=1)
    
    print("Calculating math functions...")
    func_type = random.choice([generate_parabola, generate_line, generate_absolute])
    x_values = random.sample(range(-5, 6), 3)
    x_values.sort()

    points = [{"x": x, "y": func_type(x)} for x in x_values]
    points_json = json.dumps(points)

    # Connect to the PostgreSQL database
    # We use DATABASE_URL if provided (e.g. GitHub Actions), else fallback to individual vars
    try:
        print("Connecting to DB...")
        db_url = os.getenv("DATABASE_URL")
        if db_url:
            conn = psycopg2.connect(db_url, connect_timeout=10000)
        else:
            conn = psycopg2.connect(
                host=os.getenv("DB_HOST", "db"),
                database=os.getenv("POSTGRES_DB", "grapholo_db"),
                user=os.getenv("POSTGRES_USER", "graph_user"),
                password=os.getenv("POSTGRES_PASSWORD", "graph_password"),
                connect_timeout=10
            )
        cur = conn.cursor()
        
        # Insert the puzzle, ignoring if it already exists for that date
        cur.execute("""
            INSERT INTO puzzles (puzzle_date, target_points, hint) 
            VALUES (%s, %s, %s) 
            ON CONFLICT (puzzle_date) DO NOTHING;
        """, (puzzle_date, points_json, "Generated automatically"))
        
        conn.commit()
        cur.close()
        conn.close()
        print(f"!! Successfully inserted puzzle for {puzzle_date}")
        
    except Exception as e:
        print(f"!! Failed to insert puzzle into database: {e}")

def main():
    print("Grapholo Puzzle Generator script started!")
    
    generate_and_insert_puzzle()
    
    print("Puzzle generation complete!")

if __name__ == "__main__":
    main()