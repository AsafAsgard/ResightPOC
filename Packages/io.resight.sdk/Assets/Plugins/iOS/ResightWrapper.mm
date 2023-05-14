#import <UIKit/UIKit.h>
#import <MetalKit/MetalKit.h>
#import "UnityView.h"
#import "UnityAppController.h"
#import "UnityAppController+ViewHandling.h"

#ifdef __cplusplus
extern "C"{
#endif
    void RSSetMiscParam(const char *key, void *value, int primitive_type);
#ifdef __cplusplus
}
#endif

@interface ResightAppController : UnityAppController
{
}
- (UnityView*)createUnityView;
- (void)applicationDidBecomeActive:(UIApplication*)application;

@end
@implementation ResightAppController
MTKView *_mtkView = nil;
- (UnityView*)createUnityView
{
    UnityView *view = [super createUnityView];
    CGRect  viewRect = CGRectMake(10, 10, 320, 240);
    _mtkView = [[MTKView alloc] initWithFrame:viewRect device: UnityGetMetalDevice()];

    [view addSubview: _mtkView];
    
    return view;
}

- (void)applicationDidBecomeActive:(UIApplication*)application
{
    RSSetMiscParam("metalView", (void *)&_mtkView, -1);
    [super applicationDidBecomeActive: application];
}
@end
IMPL_APP_CONTROLLER_SUBCLASS(ResightAppController);
