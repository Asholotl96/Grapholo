-- This script runs automatically the VERY FIRST time the Postgres container starts.

CREATE TABLE Users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) UNIQUE NOT NULL,
    current_streak INT DEFAULT 0,
    max_streak INT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Puzzles (
    id SERIAL PRIMARY KEY,
    puzzle_date DATE UNIQUE NOT NULL,
    
    -- JSONB is perfect for storing arrays of coordinates like [{"x": -3, "y": 0}]
    target_points JSONB NOT NULL,
    
    -- Future proofing: To restrict users to specific operators later
    allowed_functions JSONB,
    hint TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Submissions (
    id SERIAL PRIMARY KEY,
    user_id UUID REFERENCES Users(id) ON DELETE CASCADE,
    puzzle_id INT REFERENCES Puzzles(id) ON DELETE CASCADE,
    
    -- The actual string the user submitted (e.g., "-x^2/3 + 3")
    equation_used TEXT NOT NULL,
    is_successful BOOLEAN DEFAULT FALSE,
    attempts INT DEFAULT 1,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Let's insert a dummy puzzle so our API has something to fetch later!
INSERT INTO Puzzles (puzzle_date, target_points, hint)
VALUES (
    CURRENT_DATE,
    '[{"x": -3, "y": 0}, {"x": 0, "y": 3}, {"x": 3, "y": 0}]',
    'Think symmetrical, but flatter.'
);