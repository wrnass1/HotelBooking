--liquibase formatted sql

--changeset system:005-create-rooms-table
CREATE TABLE "Rooms" (
    "Id" SERIAL PRIMARY KEY,
    "HotelId" INTEGER NOT NULL,
    "RoomNumber" VARCHAR(50) NOT NULL,
    "RoomType" VARCHAR(50) NOT NULL,
    "PricePerNight" DECIMAL(18,2) NOT NULL,
    "MaxOccupancy" INTEGER NOT NULL,
    "Description" TEXT,
    "IsAvailable" BOOLEAN NOT NULL DEFAULT true,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    CONSTRAINT "FK_Rooms_Hotels" 
        FOREIGN KEY ("HotelId") 
        REFERENCES "Hotels" ("Id") 
        ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Rooms_HotelId_RoomNumber" ON "Rooms" ("HotelId", "RoomNumber");
