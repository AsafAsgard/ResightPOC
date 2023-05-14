//
//  events_types.hpp
//  sdk
//
//  Copyright © 2020 ReSight. All rights reserved.
//

#ifndef LIBRESIGHT_TYPES_HPP
#define LIBRESIGHT_TYPES_HPP

#if defined(__APPLE__)
    #include "TargetConditionals.h"
    #if TARGET_OS_IPHONE
        #include <stdint.h>
    #else
        #include <cstdint>
    #endif
#else
    #include <cstdint>
#endif

#include <simd/vector_types.h>
#include <simd/matrix_types.h>

//
// Generic types
//

typedef uint64_t RSTimeStamp; // microseconds duration (epoch is 1/1/1970, if needed)
typedef uint64_t RSAnchorID;


//
// Pose related data structures
//

struct RSPosition {
    float x;
    float y;
    float z;
};

struct RSRotation {
    float x;
    float y;
    float z;
    float w;
}; // Quaternion

struct RSPose {
    struct RSPosition pos;
    struct RSRotation rot;
};

struct RSAnchor {
    const RSAnchorID id;
    const RSAnchorID parentId;
    struct RSPose pose;
};


//
// Events' Data
//
//

enum RSAnchorType : int {
    AnchorSession = 1<<0,
    AnchorRegion = 1<<1,
    AnchorAnchor = 1<<3,
    AnchorPoint = 1<<4
};

enum RSTrackingState : int {
    Unavailable = 0,
    Normal = 1,
    Initializing = 2,
    ExcessiveMotion = 3,
    InsufFeatures = 4,
    Relocalizing = 5
};

enum RSEngineState {
    Uninitialized = -1,
    Init = 0,
    Mapping,
    Stopping,
    Stopped
};

struct RSLandmarkType {
    enum {
        Invalid                 = 0,
        
        // ReSight
        RSNone                  = (0 << 16) | 0x01,
        RSMap                   = (0 << 16) | 0x02,
        RSUserAnchor            = (0 << 16) | 0x03,

        // ArKit
        ArKitPlane              = (1 << 16) | 0x01,
        ArKitWorldMap           = (1 << 16) | 0x02,
        ArKitImage              = (1 << 16) | 0x03,
        ArKitObject             = (1 << 16) | 0x04,
        ArKitAnchor             = (1 << 16) | 0x05,
        
        // ArCore
        ArCorePlane             = (2 << 16) | 0x01,
        ArCorePoint             = (2 << 16) | 0x02,
        ArCoreAugmentedImage    = (2 << 16) | 0x03,
        ArCoreCloudAnchor       = (2 << 16) | 0x04,
        
        // ---
        LANDMARKTYPE_MAX_VALUE  = (uint32_t)(1<<31)
    } type;
};

struct RSLandmarkEvent {
    char const* userId;
    struct RSPose pose;     // pose of the landmark in the current inertial frame
    struct RSPose arPose;   // pose of the camera in the current inertial frame
    struct RSLandmarkType landmarkType;
};

struct RSMeshBlockEvent {
    uint64_t anchor_id;
    uint64_t block_id;
    struct RSPosition block_pos;
    struct RSPosition block_size;
};

struct RSEntity {
    char const* id;
    RSAnchorID parent;
    struct RSPose pose;            // pose in parent's coordinates
    unsigned long dtsize;
    uint8_t data[512];
};

struct RSPixelType {
    enum {
        U8  = 0,
        S8  = 1,
        U16 = 2,
        S16 = 3,
        S32 = 4,
        F32 = 5,
        F64 = 6,
        F16 = 7,
        PIXELTYPE_MAX_VALUE = (uint32_t)(1<<31)
    } type;
};

struct RSBuffer {
    void* buf;
    int width;
    int height;
    int stride;
    struct RSPixelType pixel_type;
};

struct RSCamera {
    struct RSPose pose;
    float intrinsics[4];
    float image_resolution[2];
    float exposure_duration;
    float exposure_offset;
};

struct RSLightEstimate {
    float ambient_intensity;
    float ambient_color_temperature;
};

struct RSFrameEvent {
    uint64_t frame_ts;
    enum RSTrackingState tracking_state;
    struct RSCamera camera;
    struct RSBuffer yplane;
    struct RSBuffer uvplane;
    struct RSBuffer depth_plane;
    struct RSBuffer depth_confidence_plane;
    struct RSBuffer humen_stencil;
    struct RSBuffer humen_depth;
    struct RSLightEstimate light_estimate;
    void* arframe;
};

struct RSLocationEvent {
    double lat;
    double lon;
    double alt;
    double heading;
    float acc_h;
    float acc_v;
    float acc_heading;
    uint64_t heading_ts;
    uint64_t gps_ts;
};

#endif //LIBRESIGHT_TYPES_HPP
