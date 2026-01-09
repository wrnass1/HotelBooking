--liquibase formatted sql

--changeset system:003-create-facilities-table
CREATE TABLE "Facilities" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL UNIQUE,
    "Description" TEXT,
    "Icon" VARCHAR(50) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX "IX_Facilities_Name" ON "Facilities" ("Name");
