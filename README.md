# Grapholo

![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Python](https://img.shields.io/badge/Python_3.11-3776AB?style=for-the-badge&logo=python&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![TailwindCSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white)

**Wordle for math nerds.** Grapholo is a daily, browser-based puzzle game where players must find the correct mathematical equation (y = f(x)) to intersect a set of daily target coordinates. 

Play the game, track your progress, and share your path to victory with Wordle-style emoji grids.

## Features

* **Real-time Graphing Engine:** Fluid, 60fps rendering using the HTML5 Canvas API and `mathjs`. Watch your curve update in real-time as you type.
* **Daily Passive Resets:** A custom Python worker continuously runs in the background, mathematically generating and inserting new puzzles at exactly midnight UTC.
* **Server-Side Anti-Cheat:** The C# API Gateway routes user submissions to a secure Python microservice utilizing `SymPy`. Math is validated mathematically on the server, completely preventing client-side spoofing.
* **Wordle-Style Gamification:** The game tracks your attempts and generates a shareable matrix representing your path to solving the puzzle (e.g., `Grapholo #1 🟥🟩🟥 ➡️ 🟩🟩🟩`).
* **High-DPI Mobile Support:** Crisp canvas rendering across all device pixel ratios.

---

## Architecture

Grapholo is built using a modern, decoupled microservices architecture orchestrated by Docker Compose:

1. **Frontend (Vanilla HTML/JS):** A lightweight, static client utilizing Tailwind CSS and the HTML5 Canvas API. Evaluates math locally for instant visual feedback.
2. **API Gateway (C# / ASP.NET Core 8):** The secure entry point for the frontend. Handles CORS, queries the PostgreSQL database via Entity Framework Core, and delegates validation to the Math Engine.
3. **Math Engine (Python / FastAPI):** A stateless microservice dedicated to secure algorithmic parsing. Uses `SymPy` to mathematically verify if a user's submitted equation genuinely hits the target coordinates.
4. **Database (PostgreSQL):** Stores daily puzzles, target coordinates, and user submissions.
5. **Puzzle Worker (Python):** A detached background process inside the Math Engine container that uses `schedule` to generate the next day's puzzle automatically.

---

## Getting Started (Local Development)

### Prerequisites
* [Docker](https://www.docker.com/products/docker-desktop) and Docker Compose installed on your machine.
* A modern web browser.

### Installation

1. **Clone the repository**
   ```text
   git clone https://github.com/Asholotl96/Grapholo.git
   cd Grapholo
   ```

2. **Spin up the Backend**
   Build and start the microservice network (Postgres, C# Gateway, Python Engine).
   ```text
   docker-compose up --build
   ```
   *The database will automatically initialize its tables and the Python worker will generate the first puzzle.*

3. **Launch the Frontend**
   Because the frontend is also containerised, just run 'localhost:5444' or a port of your choice by editing it in the docker-compose.yml file.

### Ports
* **Frontend:** Your local browser (e.g., `http://localhost:5444`)
* **C# API Gateway:** `http://localhost:8080`
* **Python Math Engine:** `http://localhost:5000`
* **PostgreSQL:** `localhost:5432`

---

## How to Play

1. **Observe the Targets:** You will see red/orange target dots on the Cartesian plane.
2. **Type an Equation:** Use standard math notation (e.g., `-x^2/3 + 3`, `sin(x) * 2`, `abs(x)`) in the input box.
3. **Hit Enter to Submit:** Once you think your curve intersects all the targets, press `Enter`. The C# server will securely validate your math.
4. **Share:** Copy your emoji grid and share your result!

## Contributing
Contributions, issues, and feature requests are welcome! Feel free to check the issues page.

P.S: I am also looking to work on this further to deploy it as a fully-fledged puzzle site. So please star the repo and look out for future updates!!
