
#import <Foundation/Foundation.h>
#import <ARKit/ARKit.h>
#import <CoreLocation/CoreLocation.h>
#include "resight/libresight.h"

@interface LocationServices : NSObject<CLLocationManagerDelegate>
+ (LocationServices * _Nonnull)instance;
- (nonnull instancetype)init;
- (void)start;
@property (nonatomic) CLLocationManager *locationManager;
@end

@implementation LocationServices
- (instancetype)init
{
    self = [super init];
    return self;
}

+ (instancetype)instance
{
    static dispatch_once_t once;
    static id sharedInstance;

    dispatch_once(&once, ^
    {
        sharedInstance = [self new];
    });

    return sharedInstance;
}

- (void)start
{
    _locationManager = [[CLLocationManager alloc] init];
    _locationManager.delegate = self;
    _locationManager.desiredAccuracy = kCLLocationAccuracyBest;
    _locationManager.activityType = CLActivityTypeOther;
    if (_locationManager.authorizationStatus == kCLAuthorizationStatusNotDetermined) {
        [_locationManager requestWhenInUseAuthorization];
    }

}

- (void)stop
{
    [_locationManager stopUpdatingLocation];
    [_locationManager stopUpdatingHeading];
}

- (void)locationManagerDidChangeAuthorization:(CLLocationManager * _Nonnull)manager
{
    if ( _locationManager.authorizationStatus == kCLAuthorizationStatusDenied) {
        NSLog(@"LocationServices are not enabled!!!!!!!!!!!!!!!!");
        assert(FALSE);
        return;
    }

    [_locationManager startUpdatingLocation];
    [_locationManager startUpdatingHeading];
}

- (void)locationManager:(CLLocationManager * _Nonnull)manager didUpdateLocations:(NSArray<CLLocation *> * _Nonnull)locations
{
    if (locations.count <= 0) {
        return;
    }
    if (manager.heading == nil) {
        return;
    }
    
    if (RSGetState() == Uninitialized) {
        return;
    }
    
    auto location = manager.location;
    
    double trueHeading = manager.heading.trueHeading;
    double headingTs = [manager.heading.timestamp timeIntervalSince1970] * 1e6;
    
    auto event = (RSLocationEvent) {
        location.coordinate.latitude,
        location.coordinate.longitude,
        location.altitude,
        trueHeading,
        static_cast<float>(location.horizontalAccuracy),
        static_cast<float>(location.verticalAccuracy),
        static_cast<float>(manager.heading.headingAccuracy),
        static_cast<uint64_t>(headingTs),
        static_cast<uint64_t>([location.timestamp timeIntervalSince1970] * 1e6)
    };
    
    RSOnLocation(static_cast<uint64_t>([[NSDate now] timeIntervalSince1970] * 1e6), event);
}

@end

extern "C" {
    void iOSLocationServicesStart() {
        [[LocationServices instance] start];
    }

    void iOSLocationServicesStop() {
        [[LocationServices instance] stop];
    }

    uint64_t iOSTimeNow() {
        return static_cast<uint64_t>([[NSDate now] timeIntervalSince1970] * 1e6);
    }
}
