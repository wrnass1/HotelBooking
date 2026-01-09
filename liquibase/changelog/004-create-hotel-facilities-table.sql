--liquibase formatted sql

--changeset system:004-create-hotel-facilities-table
CREATE TABLE "HotelFacilities" (
    "HotelId" INTEGER NOT NULL,
    "FacilityId" INTEGER NOT NULL,
    PRIMARY KEY ("HotelId", "FacilityId"),
    CONSTRAINT "FK_HotelFacilities_Hotels" 
        FOREIGN KEY ("HotelId") 
        REFERENCES "Hotels" ("Id") 
        ON DELETE CASCADE,
    CONSTRAINT "FK_HotelFacilities_Facilities" 
        FOREIGN KEY ("FacilityId") 
        REFERENCES "Facilities" ("Id") 
        ON DELETE CASCADE
);
