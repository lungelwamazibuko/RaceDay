/*
    RaceDayDB
    SQL Server Database Script
    Authentication + Admin + Organiser + Participant
*/

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

/* =========================================================
   1. USERS
   Common login and registration information
   ========================================================= */

CREATE TABLE tblUsers
(
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    PhoneNumber VARCHAR(20),
    AccountStatus VARCHAR(20) NOT NULL DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT CK_User_AccountStatus
        CHECK (AccountStatus IN ('Active', 'Inactive', 'Suspended'))
);
GO

/* =========================================================
   2. ADMIN
   ========================================================= */

CREATE TABLE tblAdmin
(
    AdminID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL UNIQUE,

    CONSTRAINT FK_Admin_User
        FOREIGN KEY (UserID)
        REFERENCES tblUsers(UserID)
);
GO

/* =========================================================
   3. ORGANISER
   ========================================================= */

CREATE TABLE tblOrganiser
(
    OrganiserID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL UNIQUE,

    CONSTRAINT FK_Organiser_User
        FOREIGN KEY (UserID)
        REFERENCES tblUsers(UserID)
);
GO

/* =========================================================
   4. PARTICIPANT
   ========================================================= */

CREATE TABLE tblParticipant
(
    ParticipantID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL UNIQUE,

    CONSTRAINT FK_Participant_User
        FOREIGN KEY (UserID)
        REFERENCES tblUsers(UserID)
);
GO

/* =========================================================
   5. EVENTS
   ========================================================= */

CREATE TABLE tblEvents
(
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL,
    EventName VARCHAR(150) NOT NULL,
    EventDate DATE NOT NULL,
    Location VARCHAR(150) NOT NULL,
    Description VARCHAR(500),

    CONSTRAINT FK_Event_Organiser
        FOREIGN KEY (OrganiserID)
        REFERENCES tblOrganiser(OrganiserID)
);
GO

/* =========================================================
   6. WEATHER
   ========================================================= */

CREATE TABLE tblWeather
(
    WeatherID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    WeatherDate DATE NOT NULL,
    Temperature DECIMAL(5,2),
    Conditions VARCHAR(100),
    WindSpeed DECIMAL(5,2),

    CONSTRAINT FK_Weather_Event
        FOREIGN KEY (EventID)
        REFERENCES tblEvents(EventID)
);
GO

/* =========================================================
   7. ROUTES
   ========================================================= */

CREATE TABLE tblRoutes
(
    RouteID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    RouteName VARCHAR(150) NOT NULL,
    DistanceKM DECIMAL(6,2) NOT NULL,
    Description VARCHAR(500),

    CONSTRAINT FK_Route_Event
        FOREIGN KEY (EventID)
        REFERENCES tblEvents(EventID),

    CONSTRAINT UQ_Route_Event
        UNIQUE (RouteID, EventID)
);
GO

/* =========================================================
   8. CATEGORY
   ========================================================= */

CREATE TABLE tblCategory
(
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    RouteID INT NOT NULL,
    CategoryName VARCHAR(100) NOT NULL,
    Gender VARCHAR(20),
    MinimumAge INT,
    MaximumAge INT,

    CONSTRAINT FK_Category_Event
        FOREIGN KEY (EventID)
        REFERENCES tblEvents(EventID),

    CONSTRAINT FK_Category_Route
        FOREIGN KEY (RouteID)
        REFERENCES tblRoutes(RouteID),

    CONSTRAINT FK_Category_EventRoute
        FOREIGN KEY (RouteID, EventID)
        REFERENCES tblRoutes(RouteID, EventID),

    CONSTRAINT CK_Category_Age
        CHECK (
            MinimumAge IS NULL
            OR MaximumAge IS NULL
            OR MinimumAge <= MaximumAge
        )
);
GO

/* =========================================================
   9. REGISTRATIONS
   ========================================================= */

CREATE TABLE tblRegistrations
(
    RegistrationID INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT NOT NULL,
    CategoryID INT NOT NULL,
    RegistrationDate DATETIME NOT NULL DEFAULT GETDATE(),
    BibNumber INT NOT NULL UNIQUE,
    RegistrationStatus VARCHAR(20) NOT NULL DEFAULT 'Registered',

    CONSTRAINT FK_Registration_Participant
        FOREIGN KEY (ParticipantID)
        REFERENCES tblParticipant(ParticipantID),

    CONSTRAINT FK_Registration_Category
        FOREIGN KEY (CategoryID)
        REFERENCES tblCategory(CategoryID),

    CONSTRAINT CK_Registration_Status
        CHECK (
            RegistrationStatus IN
            ('Registered', 'Cancelled', 'Completed')
        )
);
GO

/* =========================================================
   10. RESULTS
   One registration can have zero or one result
   ========================================================= */

CREATE TABLE tblResults
(
    ResultID INT IDENTITY(1,1) PRIMARY KEY,
    RegistrationID INT NOT NULL UNIQUE,
    FinishTime TIME NULL,
    Position INT NULL,
    ResultStatus VARCHAR(20) NOT NULL DEFAULT 'Finished',

    CONSTRAINT FK_Result_Registration
        FOREIGN KEY (RegistrationID)
        REFERENCES tblRegistrations(RegistrationID),

    CONSTRAINT CK_Result_Position
        CHECK (Position IS NULL OR Position > 0),

    CONSTRAINT CK_Result_Status
        CHECK (
            ResultStatus IN
            ('Finished', 'DNF', 'DNS', 'Disqualified')
        )
);
GO

/* =========================================================
   SAMPLE USERS
   PasswordHash values below are placeholders.
   In the application, store properly salted password hashes.
   ========================================================= */

INSERT INTO tblUsers
(
    FullName,
    Email,
    PasswordHash,
    PhoneNumber
)
VALUES
(
    'Admin User',
    'admin@raceday.co.za',
    'HASHED_PASSWORD_ADMIN',
    '0800000001'
),
(
    'Thabo Mokoena',
    'thabo@raceday.co.za',
    'HASHED_PASSWORD_THABO',
    '0800000002'
),
(
    'Lerato Dlamini',
    'lerato@raceday.co.za',
    'HASHED_PASSWORD_LERATO',
    '0800000003'
),
(
    'Sipho Nkosi',
    'sipho@raceday.co.za',
    'HASHED_PASSWORD_SIPHO',
    '0800000004'
),
(
    'Nomsa Khumalo',
    'nomsa@raceday.co.za',
    'HASHED_PASSWORD_NOMSA',
    '0800000005'
);
GO

/* =========================================================
   ASSIGN USER TYPES
   ========================================================= */

INSERT INTO tblAdmin (UserID)
VALUES (1);
GO

INSERT INTO tblOrganiser (UserID)
VALUES (2), (3);
GO

INSERT INTO tblParticipant (UserID)
VALUES (4), (5);
GO

/* =========================================================
   SAMPLE EVENTS
   ========================================================= */

INSERT INTO tblEvents
(
    OrganiserID,
    EventName,
    EventDate,
    Location,
    Description
)
VALUES
(
    1,
    'Durban Coastal Run',
    '2026-10-10',
    'Durban',
    'Annual coastal running event.'
),
(
    2,
    'Umhlanga Cycle Challenge',
    '2026-11-15',
    'Umhlanga',
    'Road cycling challenge.'
),
(
    1,
    'Durban Fun Walk',
    '2026-12-05',
    'Durban',
    'Community walking event.'
);
GO

/* =========================================================
   SAMPLE WEATHER
   ========================================================= */

INSERT INTO tblWeather
(
    EventID,
    WeatherDate,
    Temperature,
    Conditions,
    WindSpeed
)
VALUES
(
    1,
    '2026-10-10',
    23.50,
    'Partly Cloudy',
    14.20
),
(
    2,
    '2026-11-15',
    25.00,
    'Sunny',
    11.50
),
(
    3,
    '2026-12-05',
    24.00,
    'Clear',
    8.00
);
GO

/* =========================================================
   SAMPLE ROUTES
   ========================================================= */

INSERT INTO tblRoutes
(
    EventID,
    RouteName,
    DistanceKM,
    Description
)
VALUES
(
    1,
    'Coastal 10KM',
    10.00,
    '10 kilometre coastal running route.'
),
(
    1,
    'Coastal 21KM',
    21.10,
    'Half-marathon coastal route.'
),
(
    2,
    'Umhlanga 50KM',
    50.00,
    '50 kilometre cycling route.'
),
(
    3,
    'Durban 5KM Walk',
    5.00,
    '5 kilometre community walk.'
);
GO

/* =========================================================
   SAMPLE CATEGORIES
   ========================================================= */

INSERT INTO tblCategory
(
    EventID,
    RouteID,
    CategoryName,
    Gender,
    MinimumAge,
    MaximumAge
)
VALUES
(
    1,
    1,
    '10KM Open',
    'Open',
    18,
    NULL
),
(
    1,
    2,
    '21KM Open',
    'Open',
    18,
    NULL
),
(
    2,
    3,
    '50KM Cycling Open',
    'Open',
    18,
    NULL
),
(
    3,
    4,
    '5KM Family Walk',
    'Open',
    10,
    NULL
);
GO

/* =========================================================
   SAMPLE REGISTRATIONS
   ========================================================= */

INSERT INTO tblRegistrations
(
    ParticipantID,
    CategoryID,
    BibNumber,
    RegistrationStatus
)
VALUES
(
    1,
    1,
    1001,
    'Registered'
),
(
    2,
    2,
    1002,
    'Registered'
),
(
    1,
    3,
    1003,
    'Registered'
);
GO

/* =========================================================
   SAMPLE RESULTS
   RegistrationID is UNIQUE, enforcing 1:0..1
   ========================================================= */

INSERT INTO tblResults
(
    RegistrationID,
    FinishTime,
    Position,
    ResultStatus
)
VALUES
(
    1,
    '00:52:35',
    1,
    'Finished'
),
(
    2,
    '01:45:20',
    2,
    'Finished'
);
GO

/* =========================================================
   LOGIN QUERY
   ========================================================= */

-- Application should compare a securely hashed password.
SELECT
    UserID,
    FullName,
    Email,
    PasswordHash,
    AccountStatus
FROM tblUsers
WHERE Email = 'admin@raceday.co.za'
  AND AccountStatus = 'Active';
GO

/* =========================================================
   CHECK USER ROLE
   ========================================================= */

SELECT
    U.UserID,
    U.FullName,
    U.Email,
    CASE
        WHEN A.UserID IS NOT NULL THEN 'Admin'
        WHEN O.UserID IS NOT NULL THEN 'Organiser'
        WHEN P.UserID IS NOT NULL THEN 'Participant'
        ELSE 'Unknown'
    END AS UserType
FROM tblUsers U
LEFT JOIN tblAdmin A
    ON U.UserID = A.UserID
LEFT JOIN tblOrganiser O
    ON U.UserID = O.UserID
LEFT JOIN tblParticipant P
    ON U.UserID = P.UserID;
GO

/* =========================================================
   VERIFY RELATIONSHIPS
   ========================================================= */

SELECT
    E.EventID,
    E.EventName,
    O.OrganiserID,
    U.FullName AS OrganiserName
FROM tblEvents E
INNER JOIN tblOrganiser O
    ON E.OrganiserID = O.OrganiserID
INNER JOIN tblUsers U
    ON O.UserID = U.UserID;
GO

SELECT
    R.RegistrationID,
    U.FullName AS ParticipantName,
    C.CategoryName,
    R.BibNumber
FROM tblRegistrations R
INNER JOIN tblParticipant P
    ON R.ParticipantID = P.ParticipantID
INNER JOIN tblUsers U
    ON P.UserID = U.UserID
INNER JOIN tblCategory C
    ON R.CategoryID = C.CategoryID;
GO
