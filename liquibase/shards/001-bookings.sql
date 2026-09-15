--liquibase formatted sql

--changeset system:shards-001-bookings
-- Same service entity as public.Bookings on Primary; IDs come from its global sequence.
-- Rooms stay on Primary, so the relationship is resolved/checked by the backend.
CREATE TABLE public."Bookings" (
    "Id" INTEGER PRIMARY KEY,
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
    "UpdatedAt" TIMESTAMP
);
CREATE INDEX "IX_Bookings_RoomId_Dates" ON public."Bookings" ("RoomId", "CheckInDate", "CheckOutDate");
CREATE INDEX "IX_Bookings_GuestEmail" ON public."Bookings" ("GuestEmail");

-- Refuse accidental configuration changes that would route existing rows elsewhere.
CREATE TABLE public.sharding_layout (
    singleton BOOLEAN PRIMARY KEY DEFAULT true CHECK (singleton),
    fingerprint TEXT NOT NULL
);
INSERT INTO public.sharding_layout VALUES (true, 'sha256-u64be-v1|Modulo|1|shard-0,shard-1,shard-2');
