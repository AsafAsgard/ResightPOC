//
//  libResight.h
//  sdk
//
//  Copyright © 2020 ReSight. All rights reserved.
//

#ifndef LIBRESIGHT_H_
#define LIBRESIGHT_H_

#include "events_types.hpp"

#ifndef FOUNDATION_EXPORT
#ifdef __cplusplus
#define FOUNDATION_EXPORT extern "C"
#else
#define FOUNDATION_EXPORT extern
#endif
#endif

//
// Callbacks
//

typedef void (*OnStatus)(enum RSEngineState status, void *usrCtx);
typedef void (*OnAnchor)(struct RSAnchor anchor, enum RSAnchorType anchorType, uint8_t weight, const char *userdata, void *usrCtx);
typedef void (*OnAnchorActive)(struct RSAnchor anchor, void *usrCtx);

typedef void (*OnEntity)(struct RSEntity entity, void *usrCtx);

typedef void (*OnMeshBlock)(struct RSMeshBlockEvent block, void *usrCtx);
typedef void (*OnMeshExported)(const char* path);

struct RSConfiguration {
    const char *dataPath;
    const char *libPath;
    const char *developerKey;
    const char *ns;
    
    OnStatus status;
    OnAnchor anchor;
    
    OnEntity entityAdded;
    OnEntity entityRemoved;
    OnEntity entityPoseUpdated;
    OnEntity entityDataUpdated;
    
    OnMeshBlock meshBlockRemoved;
    OnMeshBlock meshBlockUpdated;
    OnMeshExported meshExported;
    
    void *usrCtx;
};
//
// API functions
//

FOUNDATION_EXPORT void RSInitialize(RSTimeStamp ts, struct RSConfiguration conf);
FOUNDATION_EXPORT void RSTearDown();

FOUNDATION_EXPORT void RSDebug(char const* s);

FOUNDATION_EXPORT void RSStop(RSTimeStamp ts);
FOUNDATION_EXPORT enum RSEngineState RSGetState();

FOUNDATION_EXPORT void RSTick(RSTimeStamp ts);
FOUNDATION_EXPORT void RSOnFrame(RSTimeStamp ts, struct RSFrameEvent frame_event);
FOUNDATION_EXPORT void RSOnLocation(RSTimeStamp ts, struct RSLocationEvent location_event);
FOUNDATION_EXPORT void RSOnLandmark(RSTimeStamp ts, struct RSLandmarkEvent landmark_event);

FOUNDATION_EXPORT struct RSAnchor RSCreateAnchor(RSAnchorID id, struct RSPose pose);
FOUNDATION_EXPORT void RSAddAnchor(struct RSAnchor anchor);
FOUNDATION_EXPORT void RSRemoveAnchor(RSAnchorID anchorId); //TODO: implement

FOUNDATION_EXPORT int RSAddEntity(struct RSAnchor parent, char const* user_id, struct RSPose pose, const void *data, unsigned int size);
FOUNDATION_EXPORT void RSRemoveEntity(struct RSAnchor parent); //TODO: implement
FOUNDATION_EXPORT void RSUpdateEntityPose(struct RSAnchor parent, struct RSPose pose);
FOUNDATION_EXPORT void RSUpdateEntityData(struct RSAnchor parent, const void *data, unsigned int size);

FOUNDATION_EXPORT void RSMeshBlock_FetchBegin(uint64_t anchor_id, uint64_t block_id, int* vertices_count, int* triangles_count);
FOUNDATION_EXPORT void RSMeshBlock_FetchEnd(uint64_t anchor_id, uint64_t block_id, void* vertex_data, void* index_data);
FOUNDATION_EXPORT void RSMeshExport(char const* file_path);

#endif
