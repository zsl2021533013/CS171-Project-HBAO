#ifndef SHADOWSHARD_AO_MASTER_HBAO_PARAMETERS_INCLUDED
#define SHADOWSHARD_AO_MASTER_HBAO_PARAMETERS_INCLUDED

half4 _HdaoParameters;
half4 _HdaoParameters2;
half4 _DepthToViewParams;

#define INTENSITY _HdaoParameters.x
#define REJECT_RADIUS _HdaoParameters.y
#define ACCEPT_RADIUS _HdaoParameters.z
#define FALLOFF _HdaoParameters.w

#define OFFSET_CORRECTION _HdaoParameters2.x
#define NORMAL_INTENSITY _HdaoParameters2.y

#endif
