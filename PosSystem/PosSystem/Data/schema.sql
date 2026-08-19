-- POS System Core Schema (SQLite)
-- Designed for: food trucks/caravans first, extensible to cafes/restaurants/retail
-- Each truck/location runs this DB locally. A central server aggregates via Sync* fields.

PRAGMA foreign_keys = ON;

-- One row per truck/location. Lets one owner run multiple trucks under one login later.
CREATE TABLE Businesses (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    BusinessType TEXT NOT NULL CHECK (BusinessType IN ('foodtruck','cafe','restaurant','retail')),
    OwnerName TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE Employees (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BusinessId INTEGER NOT NULL REFERENCES Businesses(Id),
    Name TEXT NOT NULL,
    Role TEXT NOT NULL CHECK (Role IN ('owner','manager','cashier')),
    PinCode TEXT NOT NULL,           -- simple 4-digit PIN login, not a password system
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE Categories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BusinessId INTEGER NOT NULL REFERENCES Businesses(Id),
    Name TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Products (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BusinessId INTEGER NOT NULL REFERENCES Businesses(Id),
    CategoryId INTEGER REFERENCES Categories(Id),
    Name TEXT NOT NULL,
    Price REAL NOT NULL,
    IsAvailable INTEGER NOT NULL DEFAULT 1,   -- the "86 it" sold-out toggle
    SKU TEXT,                                  -- used by retail pack for variant lookups later
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Optional modifiers (e.g. "extra shot", "no onions") — cafe/foodtruck pack
CREATE TABLE Modifiers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL REFERENCES Products(Id),
    Name TEXT NOT NULL,
    PriceDelta REAL NOT NULL DEFAULT 0
);

CREATE TABLE Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BusinessId INTEGER NOT NULL REFERENCES Businesses(Id),
    EmployeeId INTEGER REFERENCES Employees(Id),
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    Status TEXT NOT NULL DEFAULT 'completed' CHECK (Status IN ('completed','voided','refunded')),
    TotalAmount REAL NOT NULL,
    PaymentMethod TEXT NOT NULL CHECK (PaymentMethod IN ('cash','card')),
    SyncedAt TEXT               -- NULL until pushed to the central server; drives offline-first sync
);

CREATE TABLE OrderItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId INTEGER NOT NULL REFERENCES Orders(Id),
    ProductId INTEGER NOT NULL REFERENCES Products(Id),
    Quantity INTEGER NOT NULL DEFAULT 1,
    UnitPrice REAL NOT NULL,     -- snapshot price at time of sale, don't trust Products.Price for history
    Notes TEXT
);

-- Indexes for the dashboard's most common queries
CREATE INDEX idx_orders_business_created ON Orders(BusinessId, CreatedAt);
CREATE INDEX idx_orders_synced ON Orders(SyncedAt);
CREATE INDEX idx_orderitems_order ON OrderItems(OrderId);
CREATE INDEX idx_orderitems_product ON OrderItems(ProductId);
