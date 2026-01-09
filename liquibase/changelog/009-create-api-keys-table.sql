--liquibase formatted sql

--changeset system:009-create-api-keys-table
CREATE TABLE "ApiKeys" (
    "Id" SERIAL PRIMARY KEY,
    "Key" VARCHAR(256) NOT NULL UNIQUE,
    "Name" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "LastUsedAt" TIMESTAMP
);

CREATE UNIQUE INDEX "IX_ApiKeys_Key" ON "ApiKeys" ("Key");
