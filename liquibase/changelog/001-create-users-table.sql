--liquibase formatted sql

--changeset system:001-create-users-table
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Username" VARCHAR(100) NOT NULL UNIQUE,
    "Email" VARCHAR(200) NOT NULL UNIQUE,
    "PasswordHash" TEXT NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    "LastLoginAt" TIMESTAMP
);

CREATE INDEX "IX_Users_Username" ON "Users" ("Username");
CREATE INDEX "IX_Users_Email" ON "Users" ("Email");
