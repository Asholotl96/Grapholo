import json
import random
import time
import os
import psycopg2
import schedule
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
    
    func_type = random.choice([generate_parabola, generate_line, generate_absolute])
    x_values = random.sample(range(-5, 6), 3)
    x_values.sort()

    points = [{"x": x, "y": func_type(x)} for x in x_values]
    points_json = json.dumps(points)

    # Connect to the PostgreSQL database (using Docker environment variables)
    # We use default fallback values for local testing outside of Docker
    try:
        conn = psycopg2.connect(
            host=os.getenv("DB_HOST", "db"),
            database=os.getenv("POSTGRES_DB", "graphle_db"),
            user=os.getenv("POSTGRES_USER", "graph_user"),
            password=os.getenv("POSTGRES_PASSWORD", "graph_password")
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
    print("Graphle Puzzle Generator Worker started!")
    
    # Run once immediately on startup just to ensure we have a puzzle ready
    generate_and_insert_puzzle()
    
    # Schedule the job to run every day at 11:59 PM
    schedule.every().day.at("23:59").do(generate_and_insert_puzzle)

    # Continuous loop that keeps the Docker container running
    while True:
        schedule.run_pending()
        time.sleep(60) # Wake up every minute to check the time

if __name__ == "__main__":
    main()