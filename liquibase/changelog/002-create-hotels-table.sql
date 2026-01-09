--liquibase formatted sql

--changeset system:002-create-hotels-table
CREATE TABLE "Hotels" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Address" VARCHAR(500) NOT NULL,
    "City" VARCHAR(100) NOT NULL,
    "Country" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "StarRating" INTEGER NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP
);

CREATE INDEX "IX_Hotels_Name" ON "Hotels" ("Name");
