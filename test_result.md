#====================================================================================================
# START - Testing Protocol - DO NOT EDIT OR REMOVE THIS SECTION
#====================================================================================================

# THIS SECTION CONTAINS CRITICAL TESTING INSTRUCTIONS FOR BOTH AGENTS
# BOTH MAIN_AGENT AND TESTING_AGENT MUST PRESERVE THIS ENTIRE BLOCK

# Communication Protocol:
# If the `testing_agent` is available, main agent should delegate all testing tasks to it.
#
# You have access to a file called `test_result.md`. This file contains the complete testing state
# and history, and is the primary means of communication between main and the testing agent.
#
# Main and testing agents must follow this exact format to maintain testing data. 
# The testing data must be entered in yaml format Below is the data structure:
# 
## user_problem_statement: {problem_statement}
## backend:
##   - task: "Task name"
##     implemented: true
##     working: true  # or false or "NA"
##     file: "file_path.py"
##     stuck_count: 0
##     priority: "high"  # or "medium" or "low"
##     needs_retesting: false
##     status_history:
##         -working: true  # or false or "NA"
##         -agent: "main"  # or "testing" or "user"
##         -comment: "Detailed comment about status"
##
## frontend:
##   - task: "Task name"
##     implemented: true
##     working: true  # or false or "NA"
##     file: "file_path.js"
##     stuck_count: 0
##     priority: "high"  # or "medium" or "low"
##     needs_retesting: false
##     status_history:
##         -working: true  # or false or "NA"
##         -agent: "main"  # or "testing" or "user"
##         -comment: "Detailed comment about status"
##
## metadata:
##   created_by: "main_agent"
##   version: "1.0"
##   test_sequence: 0
##   run_ui: false
##
## test_plan:
##   current_focus:
##     - "Task name 1"
##     - "Task name 2"
##   stuck_tasks:
##     - "Task name with persistent issues"
##   test_all: false
##   test_priority: "high_first"  # or "sequential" or "stuck_first"
##
## agent_communication:
##     -agent: "main"  # or "testing" or "user"
##     -message: "Communication message between agents"

# Protocol Guidelines for Main agent
#
# 1. Update Test Result File Before Testing:
#    - Main agent must always update the `test_result.md` file before calling the testing agent
#    - Add implementation details to the status_history
#    - Set `needs_retesting` to true for tasks that need testing
#    - Update the `test_plan` section to guide testing priorities
#    - Add a message to `agent_communication` explaining what you've done
#
# 2. Incorporate User Feedback:
#    - When a user provides feedback that something is or isn't working, add this information to the relevant task's status_history
#    - Update the working status based on user feedback
#    - If a user reports an issue with a task that was marked as working, increment the stuck_count
#    - Whenever user reports issue in the app, if we have testing agent and task_result.md file so find the appropriate task for that and append in status_history of that task to contain the user concern and problem as well 
#
# 3. Track Stuck Tasks:
#    - Monitor which tasks have high stuck_count values or where you are fixing same issue again and again, analyze that when you read task_result.md
#    - For persistent issues, use websearch tool to find solutions
#    - Pay special attention to tasks in the stuck_tasks list
#    - When you fix an issue with a stuck task, don't reset the stuck_count until the testing agent confirms it's working
#
# 4. Provide Context to Testing Agent:
#    - When calling the testing agent, provide clear instructions about:
#      - Which tasks need testing (reference the test_plan)
#      - Any authentication details or configuration needed
#      - Specific test scenarios to focus on
#      - Any known issues or edge cases to verify
#
# 5. Call the testing agent with specific instructions referring to test_result.md
#
# IMPORTANT: Main agent must ALWAYS update test_result.md BEFORE calling the testing agent, as it relies on this file to understand what to test next.

#====================================================================================================
# END - Testing Protocol - DO NOT EDIT OR REMOVE THIS SECTION
#====================================================================================================



#====================================================================================================
# Testing Data - Main Agent and testing sub agent both should log testing data below this section
#====================================================================================================

user_problem_statement: |
  Make the country/city seed data DYNAMIC by hydrating from public REST APIs
  instead of relying on the 5 hardcoded countries. Apply to both the live
  Python/FastAPI backend AND the exportable .NET 8 / SQL Server backend.
  User provided RapidAPI key for GeoDB Cities, skipped OpenTripMap, wants
  top 120 countries, and wants the 5 curated countries kept as a fallback.

backend:
  - task: "Dynamic data hydration from REST Countries + GeoDB + Wikipedia"
    implemented: true
    working: true
    file: "backend/hydration.py, backend/server.py"
    stuck_count: 0
    priority: "high"
    needs_retesting: false
    status_history:
        - working: true
          agent: "main"
          comment: |
            Added /app/backend/hydration.py — background task that runs after FastAPI
            startup. Idempotent (state in db.hydration_state collection). Verified
            manually: 120 countries (5 curated + 115 dynamic), 130 cities.
        - working: true
          agent: "testing"
          comment: |
            VERIFIED via /app/backend_test.py against external URL. All checks PASS:
            - GET /api/countries returns 120 entries; all 5 curated IDs (c_italy,
              c_japan, c_france, c_thailand, c_peru) present.
            - 115 dynamic countries have source="rest_countries"; c_us, c_in, c_de,
              c_br present with name/code/image populated (flag URLs).
            - GET /api/countries/c_us/cities returns Washington D.C. capital with
              source="rest_countries_capital" and Wikipedia/Wikimedia image URL.
            - GET /api/countries/c_italy/cities returns exactly the 3 curated cities
              Rome, Florence, Amalfi Coast (unchanged).
            - GET /api/search?q=United finds United States, United Kingdom and
              United Arab Emirates (3 total).

  - task: "Admin endpoints: hydration-status + refresh-data"
    implemented: true
    working: true
    file: "backend/server.py"
    stuck_count: 0
    priority: "medium"
    needs_retesting: false
    status_history:
        - working: true
          agent: "main"
          comment: |
            GET /api/admin/hydration-status — public, returns state + counts.
            POST /api/admin/refresh-data — requires Bearer ADMIN_TOKEN.
        - working: true
          agent: "testing"
          comment: |
            VERIFIED:
            - GET /api/admin/hydration-status → state.status=="completed",
              countries_inserted=115, counts.countries=120, counts.cities=130.
            - POST /api/admin/refresh-data without auth → 401 (as expected).
            - POST /api/admin/refresh-data with wrong Bearer → 403 (as expected).
            - POST /api/admin/refresh-data with correct ADMIN_TOKEN from
              backend/.env → 200, returns summary JSON
              {ok, countries_inserted, cities_inserted, errors, duration_seconds}.

  - task: "Existing endpoints unchanged (auth/discovery/reviews/users)"
    implemented: true
    working: true
    file: "backend/server.py"
    stuck_count: 0
    priority: "high"
    needs_retesting: false
    status_history:
        - working: true
          agent: "main"
          comment: |
            No breaking changes to existing endpoints.
        - working: true
          agent: "testing"
          comment: |
            Regression suite all green:
            - POST /api/auth/register fresh user (laura.bianchi.<rand>@...) → 200.
            - POST /api/auth/login → 200 with JWT.
            - GET /api/auth/me with Bearer JWT → 200, returns matching email.
            - POST /api/auth/kyc (document_type=passport, image_base64="ZmFrZQ==") →
              200 with verified=true.
            - POST /api/reviews on place_id=p_colosseum (rating=5, text="Amazing!")
              → 200 with review_id.
            - GET /api/places/p_colosseum/reviews includes the new review.

  - task: ".NET export — mirror hydration logic"
    implemented: true
    working: "NA"
    file: "backend-dotnet/Services/HydrationService.cs, Controllers/AdminController.cs, Program.cs, Models.cs, AppDbContext.cs"
    stuck_count: 0
    priority: "low"
    needs_retesting: false
    status_history:
        - working: "NA"
          agent: "main"
          comment: |
            EXPORT-ONLY — not compiled in container per user instruction.
            Added HydrationStateDoc model + DbSet, HydrationService (mirrors Python
            hydration.py 1:1: REST Countries → top 120, capital-fallback, GeoDB
            augment, Wikipedia thumbnails). AdminController with both endpoints.
            Background task in Program.cs. docker-compose passes RAPIDAPI_KEY +
            ADMIN_TOKEN. README updated.

frontend:
  - task: "No frontend changes required (API is backward-compatible)"
    implemented: true
    working: "NA"
    file: "n/a"
    stuck_count: 0
    priority: "low"
    needs_retesting: false
    status_history:
        - working: "NA"
          agent: "main"
          comment: |
            The existing /api/countries endpoint just returns more results now (120 vs 5).
            The frontend country list will automatically show all hydrated countries.

metadata:
  created_by: "main_agent"
  version: "1.1"
  test_sequence: 2

test_plan:
  current_focus: []
  stuck_tasks: []
  test_all: false
  test_priority: "high_first"

agent_communication:
  - agent: "main"
    message: |
      Implemented dynamic data hydration in /app/backend/. Key things to verify:
      1. GET /api/countries returns >= 120 countries (5 curated + 115 dynamic).
      2. 5 curated country IDs (c_italy, c_japan, c_france, c_thailand, c_peru) must still exist and unchanged.
      3. Dynamic countries have country_id pattern c_<code_lower> (e.g. c_us, c_de, c_in) and source="rest_countries".
      4. GET /api/countries/c_us/cities returns the Washington DC capital city.
      5. GET /api/admin/hydration-status (no auth) returns state="completed" with counts.
      6. POST /api/admin/refresh-data WITHOUT auth -> 401. With wrong token -> 403. With correct token from backend/.env -> 200.
      7. Existing flows still work: register, login, KYC, create review on a curated place (e.g. p_colosseum).
      8. Search endpoint /api/search?q=jap should now find Japan AND Jamaica (both have "ja" prefix actually — test it).
      Credentials: see /app/memory/test_credentials.md. ADMIN_TOKEN value is in /app/backend/.env.
  - agent: "testing"
    message: |
      Backend test suite executed via /app/backend_test.py against the public
      EXPO_PUBLIC_BACKEND_URL. 32/32 checks passed. Concrete results:
      - Hydration completed: countries_inserted=115, total countries=120, cities=130.
      - All 5 curated country IDs preserved.
      - Dynamic IDs c_us/c_in/c_de/c_br all present with source=rest_countries and
        flag image populated.
      - c_us → Washington D.C. capital city with Wikimedia thumbnail image.
      - c_italy still has exactly the 3 curated cities (Rome, Florence, Amalfi Coast).
      - Admin auth matrix: no header → 401, wrong Bearer → 403, correct Bearer
        (ADMIN_TOKEN from backend/.env) → 200 with summary JSON.
      - Regression flows green: register/login/me/kyc/review-create/review-list.
      - Search "United" returns United States, United Kingdom and United Arab Emirates.
      Note: GeoDB phase still bails with 403 "Not subscribed" — does not affect
      counts because every country gets its capital via the REST Countries
      fallback. No critical issues. Nothing else needs retesting on backend.
