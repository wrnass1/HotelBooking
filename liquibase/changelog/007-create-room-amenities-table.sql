--liquibase formatted sql

--changeset system:007-create-room-amenities-table
CREATE TABLE "RoomAmenities" (
    "RoomId" INTEGER NOT NULL,
    "AmenityId" INTEGER NOT NULL,
    PRIMARY KEY ("RoomId", "AmenityId"),
    CONSTRAINT "FK_RoomAmenities_Rooms" 
        FOREIGN KEY ("RoomId") 
        REFERENCES "Rooms" ("Id") 
        ON DELETE CASCADE,
    CONSTRAINT "FK_RoomAmenities_Amenities" 
        FOREIGN KEY ("AmenityId") 
        REFERENCES "Amenities" ("Id") 
        ON DELETE CASCADE
);
