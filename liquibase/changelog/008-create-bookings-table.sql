--liquibase formatted sql

--changeset system:008-create-bookings-table
CREATE TABLE "Bookings" (
    "Id" SERIAL PRIMARY KEY,
    "RoomId" INTEGER NOT NULL,
    "GuestName" VARCHAR(200) NOT NULL,
    "GuestEmail" VARCHAR(200) NOT NULL,
    "GuestPhone" VARCHAR(50),
    "CheckInDate" DATE NOT NULL,
    "CheckOutDate" DATE NOT NULL,
    "NumberOfGuests" INTEGER NOT NULL,
    "TotalPrice" DECIMAL(18,2) NOT NULL,
    "Status" VARCHAR(50) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    CONSTRAINT "FK_Bookings_Rooms" 
        FOREIGN KEY ("RoomId") 
        REFERENCES "Rooms" ("Id") 
        ON DELETE NO ACTION
);

CREATE INDEX "IX_Bookings_GuestEmail" ON "Bookings" ("GuestEmail");
CREATE INDEX "IX_Bookings_CheckInDate" ON "Bookings" ("CheckInDate");
CREATE INDEX "IX_Bookings_CheckOutDate" ON "Bookings" ("CheckOutDate");
CREATE INDEX "IX_Bookings_RoomId" ON "Bookings" ("RoomId");
