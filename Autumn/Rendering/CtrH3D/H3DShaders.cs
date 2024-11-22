﻿using SceneGL.GLWrappers;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrH3D.Model.Material;

namespace Autumn.Rendering.CtrH3D;

internal class H3DShaders
{
    public static ShaderSource VertexShader =>
        new(
            "H3D.vert",
            ShaderType.VertexShader,
            """
            //SPICA auto-generated code
            //This code was translated from a MAESTRO Vertex Shader
            //This file was also hand modified to improve compatibility
            //Furthermore, it was modified in order to better fit Autumn's code
            #version 330 core

            layout(std140) uniform ubScene {
                vec4 WrldMtx[3];
                vec4 NormMtx[3];
                vec4 PosOffs;
                vec4 IrScale[2];
                vec4 TexcMap;
                vec4 TexMtx0[3];
                vec4 TexMtx1[3];
                vec4 TexMtx2[2];
                vec4 TexTran;
                vec4 MatAmbi;
                vec4 MatDiff;
                vec4 HslGCol;
                vec4 HslSCol;
                vec4 HslSDir;
                vec4 ProjMtx[4];
                vec4 ViewMtx[3];
                ivec4 LightCt;
                int BoolUniforms;
                int DisableVertexColor;
            };

            layout(std140) uniform ubUnivReg {
                vec4 UnivReg[60];
            };

            layout(std140) uniform ubBoneTable {
                int BoneTable[20];
            };

            //Debugging
            uniform sampler2D weightRamp1;
            uniform sampler2D weightRamp2;
            uniform int selectedBoneIndex;
            uniform int weightRampType;
            uniform int hasWeights;

            #define IsSmoSk (1 << 1)
            #define IsRgdSk (1 << 2)
            #define IsHemiL (1 << 5)
            #define IsHemiO (1 << 6)
            #define IsVertA (1 << 7)
            #define IsBoneW (1 << 8)
            #define UvMap0 (1 << 9)
            #define UvMap1 (1 << 10)
            #define UvMap2 (1 << 11)
            #define IsVertL (1 << 12)
            #define IsTex1 (1 << 13)
            #define IsTex2 (1 << 14)
            #define IsQuate (1 << 15)

            vec4 reg_temp[16];
            bvec2 reg_cmp;
            ivec2 reg_a0;
            int reg_al;

            layout(location = 0) in vec4 aPosition;
            layout(location = 1) in vec4 aNormal;
            layout(location = 2) in vec4 aTangent;
            layout(location = 3) in vec4 aColor;
            layout(location = 4) in vec4 aTexCoord0;
            layout(location = 5) in vec4 aTexCoord1;
            layout(location = 6) in vec4 aTexCoord2;
            layout(location = 7) in vec4 aBoneIndex;
            layout(location = 8) in vec4 aBoneWeight;
            layout(location = 9) in vec4 aUserAttribute0;
            layout(location = 10) in vec4 aUserAttribute1;
            layout(location = 11) in vec4 aUserAttribute2;

            out vec4 Position;
            out vec4 QuatNormal;
            out vec4 View;
            out vec4 Color;
            out vec4 TexCoord0;
            out vec4 TexCoord1;
            out vec4 TexCoord2;
            out vec4 WeightPreview;

            void proc_full_quaternion_calc_end() {
            	QuatNormal.xyzw = reg_temp[0].xyzw;
            }

            void proc_full_quaternion_calc_fallback() {
            	reg_cmp.x = reg_temp[5].z > reg_temp[5].y;
            	reg_cmp.y = reg_temp[5].y > reg_temp[5].x;
            	if (reg_cmp.x) {
            		if (reg_cmp.y) {
            			reg_temp[8].xyzw = reg_temp[13].yyzw * reg_temp[6].xxxy;
            			reg_temp[8].x = vec4(0, 1, 2, 3).y + -reg_temp[5].y;
            			reg_temp[9].xyzw = reg_temp[5].zzzz + -reg_temp[5].xxxx;
            			reg_temp[8].yzw = reg_temp[8].yzw + reg_temp[14].wxy;
            			reg_temp[8].x = reg_temp[9].x + reg_temp[8].x;
            		} else {
            			reg_cmp.x = reg_temp[5].z > reg_temp[5].x;
            			reg_cmp.y = reg_temp[5].z > reg_temp[5].x;
            			reg_temp[8].xyzw = reg_temp[13].yyzw * reg_temp[6].xxxy;
            			reg_temp[8].x = vec4(0, 1, 2, 3).y + -reg_temp[5].y;
            			if (reg_cmp.x) {
            				reg_temp[9].xyzw = reg_temp[5].zzzz + -reg_temp[5].xxxx;
            				reg_temp[8].yzw = reg_temp[8].yzw + reg_temp[14].wxy;
            				reg_temp[8].x = reg_temp[9].x + reg_temp[8].x;
            			} else {
            				reg_temp[8].xyzw = reg_temp[13].zwwy * reg_temp[6].xxxy;
            				reg_temp[8].z = vec4(0, 1, 2, 3).y + -reg_temp[5].z;
            				reg_temp[9].xyzw = reg_temp[5].xxxx + -reg_temp[5].yyyy;
            				reg_temp[8].xyw = reg_temp[8].xyw + reg_temp[14].xyw;
            				reg_temp[8].z = reg_temp[9].z + reg_temp[8].z;
            			}
            		}
            		reg_temp[8].w = -reg_temp[8].w;
            	} else {
            		if (reg_cmp.y) {
            			reg_temp[8].xyzw = reg_temp[13].yywz * reg_temp[6].xxxy;
            			reg_temp[8].y = vec4(0, 1, 2, 3).y + -reg_temp[5].z;
            			reg_temp[9].xyzw = reg_temp[5].yyyy + -reg_temp[5].xxxx;
            			reg_temp[8].xzw = reg_temp[8].xzw + reg_temp[14].wyx;
            			reg_temp[8].y = reg_temp[9].y + reg_temp[8].y;
            		} else {
            			reg_temp[8].xyzw = reg_temp[13].zwwy * reg_temp[6].xxxy;
            			reg_temp[8].z = vec4(0, 1, 2, 3).y + -reg_temp[5].z;
            			reg_temp[9].xyzw = reg_temp[5].xxxx + -reg_temp[5].yyyy;
            			reg_temp[8].xyw = reg_temp[8].xyw + reg_temp[14].xyw;
            			reg_temp[8].z = reg_temp[9].z + reg_temp[8].z;
            			reg_temp[8].w = -reg_temp[8].w;
            		}
            	}
            	reg_temp[6].xyzw = vec4(dot(reg_temp[8].xyzw, reg_temp[8].xyzw));
            	reg_temp[6].xyzw = inversesqrt(reg_temp[6].xxxx);
            	reg_temp[0].xyzw = reg_temp[8].xyzw * reg_temp[6].xyzw;
            	proc_full_quaternion_calc_end();
            }

            void proc_calc_quaternion_from_normal_end() {
            	QuatNormal.xyzw = reg_temp[0].xyzw;
            }

            void proc_gen_texcoord_sphere_reflection() {
            	reg_temp[1].xy = vec4(0.125, 0.00390625, 0.5, 0.25).zz;
            	reg_temp[1].zw = vec4(0, 1, 2, 3).xx;
            	reg_temp[6].xyzw = reg_temp[14].xyzw * reg_temp[1].xyzw + reg_temp[1].xyzw;
            	reg_temp[6].zw = vec4(0, 1, 2, 3).yy;
            }

            void proc_gen_texcoord_reflection() {
            	reg_temp[2].xyzw = -reg_temp[15].xyzw;
            	reg_temp[2].w = dot(reg_temp[2].xyz, reg_temp[2].xyz);
            	reg_temp[2].w = inversesqrt(reg_temp[2].w);
            	reg_temp[2].xyzw = reg_temp[2].xyzw * reg_temp[2].wwww;
            	reg_temp[1].xyzw = vec4(dot(reg_temp[2].xyz, reg_temp[14].xyz));
            	reg_temp[1].xyzw = reg_temp[1].xyzw + reg_temp[1].xyzw;
            	reg_temp[6].xyzw = reg_temp[1].xyzw * reg_temp[14].xyzw + -reg_temp[2].xyzw;
            }

            void proc_get_texcoord_source() {
            	reg_cmp.x = vec4(0, 1, 2, 3).y == reg_temp[0].x;
            	reg_cmp.y = vec4(0, 1, 2, 3).z == reg_temp[0].y;
            	if (!reg_cmp.x && !reg_cmp.y) {
            		reg_temp[6].xy = IrScale[1].xx * aTexCoord0.xy;
            	} else {
            		if (reg_cmp.x && !reg_cmp.y) {
            			reg_temp[6].xy = IrScale[1].yy * aTexCoord1.xy;
            		} else {
            			reg_temp[6].xy = IrScale[1].zz * aTexCoord2.xy;
            		}
            	}
            	reg_temp[6].zw = vec4(0, 1, 2, 3).yy;
            }

            void proc_calc_hemisphere_lighting() {
            	reg_temp[1].xyzw = vec4(dot(HslSDir.xyz, reg_temp[14].xyz));
            	reg_temp[2].xyzw = HslSDir.wwww;
            	reg_temp[1].xyzw = reg_temp[1].xyzw * reg_temp[2].xyzw + reg_temp[2].xyzw;
            	reg_temp[3].xyzw = HslGCol.xyzw;
            	reg_temp[2].xyzw = HslSCol.xyzw + -reg_temp[3].xyzw;
            	reg_temp[4].xyzw = reg_temp[2].xyzw * reg_temp[1].xyzw + reg_temp[3].xyzw;
            	if ((BoolUniforms & IsHemiO) != 0) {
            		reg_temp[4].xyzw = reg_temp[4].xyzw * reg_temp[9].wwww;
            	}
            	reg_temp[9].xyz = reg_temp[4].xyz * MatDiff.xyz + reg_temp[9].xyz;
            	reg_temp[8].x = vec4(0, 1, 2, 3).y;
            }

            void proc_calc_vertex_lighting() {
            	reg_temp[1].xyzw = MatAmbi.xyzw;
            	reg_temp[2].xyzw = MatDiff.xyzw;
            	reg_temp[3].xyzw = vec4(0, 1, 2, 3).xxxx;
            	for (reg_al = LightCt.y; reg_al <= LightCt.x; reg_al += LightCt.z) {
            		reg_a0.x = int(reg_temp[3].x);
            		reg_temp[4].x = UnivReg[56 + reg_a0.x].w;
            		reg_temp[4].y = UnivReg[58 + reg_a0.x].w;
            		reg_cmp.x = vec4(0, 1, 2, 3).x == reg_temp[4].x;
            		reg_cmp.y = vec4(0, 1, 2, 3).y == reg_temp[4].y;
            		if (reg_cmp.x) {
            			reg_temp[6].x = dot(UnivReg[56 + reg_a0.x].xyz, reg_temp[14].xyz);
            			reg_temp[6].y = vec4(0, 1, 2, 3).y;
            		} else {
            			reg_temp[4].xyzw = UnivReg[56 + reg_a0.x].xyzw + -reg_temp[15].xyzw;
            			reg_temp[6].y = vec4(0, 1, 2, 3).y;
            			if (reg_cmp.y) {
            				reg_temp[5].x = vec4(0, 1, 2, 3).y;
            				reg_temp[5].z = dot(reg_temp[4].xyz, reg_temp[4].xyz);
            				reg_temp[5].y = reg_temp[5].z * reg_temp[5].z;
            				reg_temp[6].y = dot(UnivReg[58 + reg_a0.x].xyz, reg_temp[5].xyz);
            				reg_temp[6].y = 1 / reg_temp[6].y;
            			}
            			reg_temp[5].xyzw = UnivReg[57 + reg_a0.x].xyzw;
            			reg_cmp.x = vec4(0, 1, 2, 3).y == reg_temp[5].w;
            			reg_cmp.y = vec4(0, 1, 2, 3).y == reg_temp[5].w;
            			reg_temp[4].w = dot(reg_temp[4].xyz, reg_temp[4].xyz);
            			reg_temp[4].w = inversesqrt(reg_temp[4].w);
            			reg_temp[4].xyzw = reg_temp[4].xyzw * reg_temp[4].wwww;
            			if (reg_cmp.x) {
            				reg_temp[5].x = dot(UnivReg[57 + reg_a0.x].xyz, -reg_temp[4].xyz);
            				reg_temp[5].y = reg_temp[5].x < reg_temp[5].y ? 1 : 0;
            				reg_cmp.x = vec4(0, 1, 2, 3).y == reg_temp[5].x;
            				reg_cmp.y = vec4(0, 1, 2, 3).y == reg_temp[5].y;
            				if (reg_cmp.y) {
            					reg_temp[5].x = vec4(0, 1, 2, 3).x;
            				} else {
            					reg_temp[5].y = UnivReg[59 + reg_a0.x].x * reg_temp[5].y;
            				}
            				reg_temp[6].y = reg_temp[6].y * reg_temp[5].x;
            			}
            			reg_temp[6].x = dot(reg_temp[14].xyz, reg_temp[4].xyz);
            		}
            		reg_cmp.x = vec4(0, 1, 2, 3).x == reg_temp[6].x;
            		reg_cmp.y = vec4(0, 1, 2, 3).x < reg_temp[6].y;
            		if (reg_cmp.y) {
            			reg_temp[6].x = max(vec4(0, 1, 2, 3).x, reg_temp[6].x);
            			reg_temp[9].xyz = reg_temp[1].xyz * UnivReg[54 + reg_a0.x].xyz + reg_temp[9].xyz;
            			reg_temp[4].xyzw = UnivReg[55 + reg_a0.x].xyzw * reg_temp[2].xyzw;
            			reg_temp[5].xyz = reg_temp[6].xxx * reg_temp[4].xyz;
            			reg_temp[5].xyz = reg_temp[6].yyy * reg_temp[5].xyz;
            			reg_temp[9].xyz = reg_temp[9].xyz + reg_temp[5].xyz;
            			reg_temp[9].w = reg_temp[9].w + reg_temp[4].w;
            		}
            		reg_temp[3].xyzw = -vec4(3, 4, 5, 6).wwww + reg_temp[3].xyzw;
                    if (LightCt.z == 0) break;
            	}
            	reg_temp[8].x = vec4(0, 1, 2, 3).y;
            }

            void proc_blend_vertex_p() {
            	reg_a0.x = int(reg_temp[1].x);
            	reg_temp[3].x = dot(UnivReg[0 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].y = dot(UnivReg[1 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].z = dot(UnivReg[2 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[7].xyzw = reg_temp[1].wwww * reg_temp[3].xyzw + reg_temp[7].xyzw;
            }

            void proc_calc_quaternion_from_tangent() {
            	reg_temp[6].x = dot(reg_temp[14].xyz, reg_temp[14].xyz);
            	reg_temp[7].x = dot(reg_temp[12].xyz, reg_temp[12].xyz);
            	reg_temp[6].x = inversesqrt(reg_temp[6].x);
            	reg_temp[7].x = inversesqrt(reg_temp[7].x);
            	reg_temp[14].xyz = reg_temp[14].xyz * reg_temp[6].xxx;
            	reg_temp[12].xyz = reg_temp[12].xyz * reg_temp[7].xxx;
            	reg_temp[13].xyz = reg_temp[13].xyz * reg_temp[6].xxx;
            	reg_temp[11].xyz = reg_temp[11].xyz * reg_temp[7].xxx;
            	reg_temp[0].xyzw = vec4(0, 1, 2, 3).yxxx;
            	reg_temp[13].xyz = reg_temp[13].xyz * reg_temp[6].xxx;
            	reg_temp[11].xyz = reg_temp[11].xyz * reg_temp[7].xxx;
            	reg_temp[5].xyzw = reg_temp[14].yzxx * reg_temp[13].zxyy;
            	reg_temp[5].xyzw = -reg_temp[13].yzxx * reg_temp[14].zxyy + reg_temp[5].xyzw;
            	reg_temp[5].w = dot(reg_temp[5].xyz, reg_temp[5].xyz);
            	reg_temp[5].w = inversesqrt(reg_temp[5].w);
            	reg_temp[5].xyzw = reg_temp[5].xyzw * reg_temp[5].wwww;
            	reg_temp[6].w = reg_temp[14].z + reg_temp[5].y;
            	reg_temp[13].xyzw = reg_temp[5].yzxx * reg_temp[14].zxyy;
            	reg_temp[13].xyzw = -reg_temp[14].yzxx * reg_temp[5].zxyy + reg_temp[13].xyzw;
            	reg_temp[6].w = reg_temp[13].x + reg_temp[6].w;
            	reg_temp[13].w = reg_temp[5].z;
            	reg_temp[5].z = reg_temp[13].x;
            	reg_temp[6].w = vec4(0, 1, 2, 3).y + reg_temp[6].w;
            	reg_temp[14].w = reg_temp[5].x;
            	reg_temp[5].x = reg_temp[14].z;
            	reg_cmp.x = vec4(0.125, 0.00390625, 0.5, 0.25).y < reg_temp[6].w;
            	reg_cmp.y = vec4(0.125, 0.00390625, 0.5, 0.25).y < reg_temp[6].w;
            	reg_temp[6].x = vec4(0, 1, 2, 3).y;
            	reg_temp[6].y = -vec4(0, 1, 2, 3).y;
            	if (!reg_cmp.x) { //Jump
            		proc_full_quaternion_calc_fallback();
            		return;
            	}
            	reg_temp[7].xz = reg_temp[13].wy + -reg_temp[14].yw;
            	reg_temp[7].y = reg_temp[14].x + -reg_temp[13].z;
            	reg_temp[7].w = reg_temp[6].w;
            	reg_temp[6].xyzw = vec4(dot(reg_temp[7].xyzw, reg_temp[7].xyzw));
            	reg_temp[6].xyzw = inversesqrt(reg_temp[6].xxxx);
            	reg_temp[0].xyzw = reg_temp[7].xyzw * reg_temp[6].xyzw;
            	if (true) { //Jump
            		proc_full_quaternion_calc_end();
            		return;
            	}
            	proc_full_quaternion_calc_fallback();
            }

            void proc_blend_vertex_pnt() {
            	reg_a0.x = int(reg_temp[1].x);
            	reg_temp[3].x = dot(UnivReg[0 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].y = dot(UnivReg[1 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].z = dot(UnivReg[2 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[4].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[4].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[4].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[5].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[13].xyz);
            	reg_temp[5].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[13].xyz);
            	reg_temp[5].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[13].xyz);
            	reg_temp[7].xyzw = reg_temp[1].wwww * reg_temp[3].xyzw + reg_temp[7].xyzw;
            	reg_temp[12].xyzw = reg_temp[1].wwww * reg_temp[4].xyzw + reg_temp[12].xyzw;
            	reg_temp[11].xyzw = reg_temp[1].wwww * reg_temp[5].xyzw + reg_temp[11].xyzw;
            }

            void proc_calc_quaternion_from_normal() {
            	reg_temp[6].x = dot(reg_temp[14].xyz, reg_temp[14].xyz);
            	reg_temp[7].x = dot(reg_temp[12].xyz, reg_temp[12].xyz);
            	reg_temp[6].x = inversesqrt(reg_temp[6].x);
            	reg_temp[7].x = inversesqrt(reg_temp[7].x);
            	reg_temp[14].xyz = reg_temp[14].xyz * reg_temp[6].xxx;
            	reg_temp[12].xyz = reg_temp[12].xyz * reg_temp[7].xxx;
            	reg_temp[0].xyzw = vec4(0, 1, 2, 3).yxxx;
            	reg_temp[4].xyzw = vec4(0, 1, 2, 3).yyyy + reg_temp[14].zzzz;
            	reg_temp[4].xyzw = vec4(0.125, 0.00390625, 0.5, 0.25).zzzz * reg_temp[4].xyzw;
            	reg_cmp.x = vec4(0, 1, 2, 3).x >= reg_temp[4].x;
            	reg_cmp.y = vec4(0, 1, 2, 3).x >= reg_temp[4].x;
            	reg_temp[4].xyzw = inversesqrt(reg_temp[4].xxxx);
            	reg_temp[5].xyzw = vec4(0.125, 0.00390625, 0.5, 0.25).zzzz * reg_temp[14].xyzw;
            	if (reg_cmp.x) { //Jump
            		proc_calc_quaternion_from_normal_end();
            		return;
            	}
            	reg_temp[0].z = 1 / reg_temp[4].x;
            	reg_temp[0].xy = reg_temp[5].xy * reg_temp[4].xy;
            	proc_calc_quaternion_from_normal_end();
            }

            void proc_blend_vertex_pn() {
            	reg_a0.x = int(reg_temp[1].x);
            	reg_temp[3].x = dot(UnivReg[0 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].y = dot(UnivReg[1 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[3].z = dot(UnivReg[2 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            	reg_temp[4].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[4].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[4].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[14].xyz);
            	reg_temp[7].xyzw = reg_temp[1].wwww * reg_temp[3].xyzw + reg_temp[7].xyzw;
            	reg_temp[12].xyzw = reg_temp[1].wwww * reg_temp[4].xyzw + reg_temp[12].xyzw;
            }

            void proc_calc_texcoord2() {
            	reg_temp[0].xy = TexcMap.zz;
            	if ((BoolUniforms & UvMap2) != 0) {
            		proc_get_texcoord_source();
            		reg_temp[5].x = dot(TexMtx2[0].xywz, reg_temp[6].xyzw);
            		reg_temp[5].y = dot(TexMtx2[1].xywz, reg_temp[6].xyzw);
            		TexCoord2.xyzw = reg_temp[5].xyzw;
            	} else {
            		reg_temp[6].zw = vec4(0, 1, 2, 3).yy;
            		reg_temp[5].zw = reg_temp[6].ww;
            		proc_gen_texcoord_sphere_reflection();
            		reg_temp[5].x = dot(TexMtx2[0].xyzw, reg_temp[6].xyzw);
            		reg_temp[5].y = dot(TexMtx2[1].xyzw, reg_temp[6].xyzw);
            		TexCoord2.xyzw = reg_temp[5].xyzw;
            	}
            }

            void proc_calc_texcoord1() {
            	reg_temp[0].xy = TexcMap.yy;
            	if ((BoolUniforms & UvMap1) != 0) {
            		proc_get_texcoord_source();
            		reg_temp[4].x = dot(TexMtx1[0].xywz, reg_temp[6].xyzw);
            		reg_temp[4].y = dot(TexMtx1[1].xywz, reg_temp[6].xyzw);
            		TexCoord1.xyzw = reg_temp[4].xyzw;
            	} else {
            		reg_cmp.x = vec4(3, 4, 5, 6).x == reg_temp[0].x;
            		reg_cmp.y = vec4(3, 4, 5, 6).y == reg_temp[0].y;
            		reg_temp[6].zw = vec4(0, 1, 2, 3).yy;
            		if (!reg_cmp.x && !reg_cmp.y) {
            			reg_temp[6].xyzw = reg_temp[10].xyzw;
            			reg_temp[4].x = dot(TexMtx1[0].xyzw, reg_temp[6].xyzw);
            			reg_temp[4].y = dot(TexMtx1[1].xyzw, reg_temp[6].xyzw);
            			reg_temp[4].z = dot(TexMtx1[2].xyzw, reg_temp[6].xyzw);
            			reg_temp[4].xy = TexTran.xy + reg_temp[4].xy;
            		} else {
            			proc_gen_texcoord_sphere_reflection();
            			reg_temp[4].x = dot(TexMtx1[0].xyzw, reg_temp[6].xyzw);
            			reg_temp[4].y = dot(TexMtx1[1].xyzw, reg_temp[6].xyzw);
            		}
            		TexCoord1.xyzw = reg_temp[4].xyzw;
            	}
            }

            void proc_calc_texcoord0() {
            	reg_temp[0].xy = TexcMap.xx;
            	if ((BoolUniforms & UvMap0) != 0) {
            		proc_get_texcoord_source();
            		reg_temp[3].x = dot(TexMtx0[0].xywz, reg_temp[6].xyzw);
            		reg_temp[3].y = dot(TexMtx0[1].xywz, reg_temp[6].xyzw);
            		reg_temp[3].zw = vec4(0, 1, 2, 3).xx;
            		TexCoord0.xyzw = reg_temp[3].xyzw;
            	} else {
            		reg_cmp.x = vec4(3, 4, 5, 6).x == reg_temp[0].x;
            		reg_cmp.y = vec4(3, 4, 5, 6).y == reg_temp[0].y;
            		reg_temp[6].zw = vec4(0, 1, 2, 3).yy;
            		if (!reg_cmp.x && !reg_cmp.y) {
            			reg_temp[6].xyzw = reg_temp[10].xyzw;
            			reg_temp[3].x = dot(TexMtx0[0].xyzw, reg_temp[6].xyzw);
            			reg_temp[3].y = dot(TexMtx0[1].xyzw, reg_temp[6].xyzw);
            			reg_temp[3].z = dot(TexMtx0[2].xyzw, reg_temp[6].xyzw);
            			reg_temp[0].xy = TexTran.xy * reg_temp[3].zz;
            			reg_temp[3].xy = reg_temp[3].xy + reg_temp[0].xy;
            		} else {
            			if (reg_cmp.x && !reg_cmp.y) {
            				proc_gen_texcoord_reflection();
            				reg_temp[3].x = dot(TexMtx0[0].xyz, reg_temp[6].xyz);
            				reg_temp[3].y = dot(TexMtx0[1].xyz, reg_temp[6].xyz);
            				reg_temp[3].z = dot(TexMtx0[2].xyz, reg_temp[6].xyz);
            			} else {
            				proc_gen_texcoord_sphere_reflection();
            				reg_temp[3].x = dot(TexMtx0[0].xyzw, reg_temp[6].xyzw);
            				reg_temp[3].y = dot(TexMtx0[1].xyzw, reg_temp[6].xyzw);
            			}
            		}
            		TexCoord0.xyzw = reg_temp[3].xyzw;
            	}
            }

            void proc_calc_color() {
            	reg_temp[8].xy = vec4(0, 1, 2, 3).xx;
            	reg_temp[0].y = IrScale[0].w;
            	reg_cmp.x = vec4(0, 1, 2, 3).x != reg_temp[0].x;
            	reg_cmp.y = vec4(0, 1, 2, 3).x != reg_temp[0].y;
            	reg_temp[9].xyz = vec4(0, 1, 2, 3).xxx;
            	reg_temp[9].w = MatDiff.w;
            	if (reg_cmp.y) {
            		reg_temp[0].xyzw = IrScale[0].wwww * aColor.xyzw;
            		if ((BoolUniforms & IsVertA) != 0) {
            			reg_temp[9].w = reg_temp[9].w * reg_temp[0].w;
            		}
            		reg_temp[9].xyz = MatAmbi.www * reg_temp[0].xyz;
            		reg_temp[8].x = vec4(0, 1, 2, 3).y;
            	}
            	if ((BoolUniforms & IsVertL) != 0) proc_calc_vertex_lighting();
            	if ((BoolUniforms & IsHemiL) != 0) proc_calc_hemisphere_lighting();
            	reg_cmp.x = vec4(0, 1, 2, 3).x == reg_temp[8].x;
            	reg_cmp.y = vec4(0, 1, 2, 3).x == reg_temp[8].y;
            	if (reg_cmp.x && reg_cmp.y) {
            		reg_temp[9].xyzw = MatDiff.xyzw;
            	}
            	Color.xyzw = max(vec4(0, 1, 2, 3).xxxx, reg_temp[9].xyzw);
            	if (DisableVertexColor == 1)
            		Color.xyzw = vec4(1);
            }

            void proc_transform_matrix() {
            	reg_temp[15].xyz = IrScale[0].xxx * aPosition.xyz;
            	reg_temp[14].xyz = IrScale[0].yyy * aNormal.xyz;
            	reg_temp[13].xyz = IrScale[0].zzz * aTangent.xyz;
            	reg_temp[15].xyz = PosOffs.xyz + reg_temp[15].xyz;
            	reg_temp[15].w = vec4(0, 1, 2, 3).y;
            	if ((BoolUniforms & IsSmoSk) != 0) {
            		reg_temp[0].xyzw = IrScale[0].xyzw;
            		reg_cmp.x = vec4(0, 1, 2, 3).x != reg_temp[0].y;
            		reg_cmp.y = vec4(0, 1, 2, 3).x != reg_temp[0].z;
            		reg_temp[7].xyzw = vec4(0, 1, 2, 3).xxxx;
            		reg_temp[12].xyzw = vec4(0, 1, 2, 3).xxxx;
            		reg_temp[11].xyzw = vec4(0, 1, 2, 3).xxxx;
            		reg_temp[2].xyzw = vec4(0, 1, 2, 3).wwww * min(aBoneIndex.xyzw, vec4(19));
            		if (reg_cmp.x && !reg_cmp.y) {
            			reg_cmp.x = vec4(0, 1, 2, 3).x != aBoneWeight.z;
            			reg_cmp.y = vec4(0, 1, 2, 3).x != aBoneWeight.w;
            			reg_temp[1].xy = reg_temp[2].xx;
            			reg_temp[1].w = IrScale[1].w * aBoneWeight.x;
            			proc_blend_vertex_pn();
            			reg_temp[1].xy = reg_temp[2].yy;
            			reg_temp[1].w = IrScale[1].w * aBoneWeight.y;
            			proc_blend_vertex_pn();
            			reg_temp[1].xy = reg_temp[2].zz;
            			reg_temp[1].w = IrScale[1].w * aBoneWeight.z;
            			if (reg_cmp.x) proc_blend_vertex_pn();
            			if ((BoolUniforms & IsBoneW) != 0) {
            				reg_temp[1].xy = reg_temp[2].ww;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.w;
            				if (reg_cmp.y) proc_blend_vertex_pn();
            			}
            			reg_temp[7].w = vec4(0, 1, 2, 3).y;
            			reg_temp[10].x = dot(WrldMtx[0].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].y = dot(WrldMtx[1].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].z = dot(WrldMtx[2].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].w = vec4(0, 1, 2, 3).y;
            			reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].w = vec4(0, 1, 2, 3).y;
            			reg_temp[14].x = dot(NormMtx[0].xyz, reg_temp[12].xyz);
            			reg_temp[14].y = dot(NormMtx[1].xyz, reg_temp[12].xyz);
            			reg_temp[14].z = dot(NormMtx[2].xyz, reg_temp[12].xyz);
            			proc_calc_quaternion_from_normal();
            		} else {
            			if (reg_cmp.x && reg_cmp.y) {
            				reg_cmp.x = vec4(0, 1, 2, 3).x != aBoneWeight.z;
            				reg_cmp.y = vec4(0, 1, 2, 3).x != aBoneWeight.w;
            				reg_temp[1].xy = reg_temp[2].xx;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.x;
            				proc_blend_vertex_pnt();
            				reg_temp[1].xy = reg_temp[2].yy;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.y;
            				proc_blend_vertex_pnt();
            				reg_temp[1].xy = reg_temp[2].zz;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.z;
            				if (reg_cmp.x) proc_blend_vertex_pnt();
            				if ((BoolUniforms & IsBoneW) != 0) {
            					reg_temp[1].xy = reg_temp[2].ww;
            					reg_temp[1].w = IrScale[1].w * aBoneWeight.w;
            					if (reg_cmp.y) proc_blend_vertex_pnt();
            				}
            				reg_temp[7].w = vec4(0, 1, 2, 3).y;
            				reg_temp[10].x = dot(WrldMtx[0].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].y = dot(WrldMtx[1].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].z = dot(WrldMtx[2].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].w = vec4(0, 1, 2, 3).y;
            				reg_temp[13].x = dot(NormMtx[0].xyz, reg_temp[11].xyz);
            				reg_temp[13].y = dot(NormMtx[1].xyz, reg_temp[11].xyz);
            				reg_temp[13].z = dot(NormMtx[2].xyz, reg_temp[11].xyz);
            				reg_temp[14].x = dot(NormMtx[0].xyz, reg_temp[12].xyz);
            				reg_temp[14].y = dot(NormMtx[1].xyz, reg_temp[12].xyz);
            				reg_temp[14].z = dot(NormMtx[2].xyz, reg_temp[12].xyz);
            				reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].w = vec4(0, 1, 2, 3).y;
            				proc_calc_quaternion_from_tangent();
            			} else {
            				reg_cmp.x = vec4(0, 1, 2, 3).x != aBoneWeight.z;
            				reg_cmp.y = vec4(0, 1, 2, 3).x != aBoneWeight.w;
            				reg_temp[1].xy = reg_temp[2].xx;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.x;
            				proc_blend_vertex_p();
            				reg_temp[1].xy = reg_temp[2].yy;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.y;
            				proc_blend_vertex_p();
            				reg_temp[1].xy = reg_temp[2].zz;
            				reg_temp[1].w = IrScale[1].w * aBoneWeight.z;
            				if (reg_cmp.x) proc_blend_vertex_p();
            				if ((BoolUniforms & IsBoneW) != 0) {
            					reg_temp[1].xy = reg_temp[2].ww;
            					reg_temp[1].w = IrScale[1].w * aBoneWeight.w;
            					if (reg_cmp.y) proc_blend_vertex_p();
            				}
            				reg_temp[7].w = vec4(0, 1, 2, 3).y;
            				reg_temp[10].x = dot(WrldMtx[0].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].y = dot(WrldMtx[1].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].z = dot(WrldMtx[2].xyzw, reg_temp[7].xyzw);
            				reg_temp[10].w = vec4(0, 1, 2, 3).y;
            				reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].w = vec4(0, 1, 2, 3).y;
            				QuatNormal.xyzw = vec4(0, 1, 2, 3).xxxx;
            			}
            		}
            		View.xyzw = -reg_temp[15].xyzw;
            		Position.x = dot(ProjMtx[0].xyzw, reg_temp[15].xyzw);
            		Position.y = dot(ProjMtx[1].xyzw, reg_temp[15].xyzw);
            		Position.z = dot(ProjMtx[2].xyzw, reg_temp[15].xyzw);
            		Position.w = dot(ProjMtx[3].xyzw, reg_temp[15].xyzw);
            	} else {
            		reg_temp[0].xyzw = IrScale[0].xyzw;
            		reg_cmp.x = vec4(0, 1, 2, 3).x != reg_temp[0].y;
            		reg_cmp.y = vec4(0, 1, 2, 3).x != reg_temp[0].z;
            		if ((BoolUniforms & IsRgdSk) != 0) {
            			reg_temp[1].x = vec4(0, 1, 2, 3).w * min(aBoneIndex.x, 19);
            			reg_a0.x = int(reg_temp[1].x);
            			reg_temp[7].x = dot(UnivReg[0 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            			reg_temp[7].y = dot(UnivReg[1 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            			reg_temp[7].z = dot(UnivReg[2 + reg_a0.x].xyzw, reg_temp[15].xyzw);
            			reg_temp[7].w = vec4(0, 1, 2, 3).y;
            			reg_temp[10].x = dot(WrldMtx[0].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].y = dot(WrldMtx[1].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].z = dot(WrldMtx[2].xyzw, reg_temp[7].xyzw);
            			reg_temp[10].w = vec4(0, 1, 2, 3).y;
            		} else {
            			reg_a0.x = int(vec4(0, 1, 2, 3).x);
            			reg_temp[10].x = dot(UnivReg[0].xyzw, reg_temp[15].xyzw);
            			reg_temp[10].y = dot(UnivReg[1].xyzw, reg_temp[15].xyzw);
            			reg_temp[10].z = dot(UnivReg[2].xyzw, reg_temp[15].xyzw);
            			reg_temp[10].w = vec4(0, 1, 2, 3).y;
            		}
            		if (reg_cmp.x && !reg_cmp.y) {
            			reg_temp[12].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[14].xyz);
            			reg_temp[12].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[14].xyz);
            			reg_temp[12].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[14].xyz);
            			reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            			reg_temp[15].w = vec4(0, 1, 2, 3).y;
            			reg_temp[14].x = dot(NormMtx[0].xyz, reg_temp[12].xyz);
            			reg_temp[14].y = dot(NormMtx[1].xyz, reg_temp[12].xyz);
            			reg_temp[14].z = dot(NormMtx[2].xyz, reg_temp[12].xyz);
            			proc_calc_quaternion_from_normal();
            		} else {
            			if (reg_cmp.x && reg_cmp.y) {
            				reg_temp[12].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[14].xyz);
            				reg_temp[12].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[14].xyz);
            				reg_temp[12].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[14].xyz);
            				reg_temp[11].x = dot(UnivReg[0 + reg_a0.x].xyz, reg_temp[13].xyz);
            				reg_temp[11].y = dot(UnivReg[1 + reg_a0.x].xyz, reg_temp[13].xyz);
            				reg_temp[11].z = dot(UnivReg[2 + reg_a0.x].xyz, reg_temp[13].xyz);
            				reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].w = vec4(0, 1, 2, 3).y;
            				reg_temp[14].x = dot(NormMtx[0].xyz, reg_temp[12].xyz);
            				reg_temp[14].y = dot(NormMtx[1].xyz, reg_temp[12].xyz);
            				reg_temp[14].z = dot(NormMtx[2].xyz, reg_temp[12].xyz);
            				reg_temp[13].x = dot(NormMtx[0].xyz, reg_temp[11].xyz);
            				reg_temp[13].y = dot(NormMtx[1].xyz, reg_temp[11].xyz);
            				reg_temp[13].z = dot(NormMtx[2].xyz, reg_temp[11].xyz);
            				proc_calc_quaternion_from_tangent();
            			} else {
            				reg_temp[15].x = dot(ViewMtx[0].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].y = dot(ViewMtx[1].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].z = dot(ViewMtx[2].xyzw, reg_temp[10].xyzw);
            				reg_temp[15].w = vec4(0, 1, 2, 3).y;
            				QuatNormal.xyzw = vec4(0, 1, 2, 3).xxxx;
            			}
            		}
            		View.xyzw = -reg_temp[15].xyzw;
            		Position.x = dot(ProjMtx[0].xyzw, reg_temp[15].xyzw);
            		Position.y = dot(ProjMtx[1].xyzw, reg_temp[15].xyzw);
            		Position.z = dot(ProjMtx[2].xyzw, reg_temp[15].xyzw);
            		Position.w = dot(ProjMtx[3].xyzw, reg_temp[15].xyzw);
            	}
            }

            vec3 BoneWeightColor(float weights)
            {
            	float rampInputLuminance = weights;
            	rampInputLuminance = clamp((rampInputLuminance), 0.001, 0.999);
                if (weightRampType == 1) // Greyscale
                    return vec3(weights);
                else if (weightRampType == 2) // Color 1
            	   return texture(weightRamp1, vec2(1 - rampInputLuminance, 0.50)).rgb;
                else // Color 2
                    return texture(weightRamp2, vec2(1 - rampInputLuminance, 0.50)).rgb;
            }

            float BoneWeightDisplay(ivec4 index)
            {
                float weight = 0;
                if (selectedBoneIndex == BoneTable[index.x])
                    weight += aBoneWeight.x;
                if (selectedBoneIndex == BoneTable[index.y])
                    weight += aBoneWeight.y;
                if (selectedBoneIndex == BoneTable[index.z])
                    weight += aBoneWeight.z;
                if (selectedBoneIndex == BoneTable[index.w])
                    weight += aBoneWeight.w;

                if (selectedBoneIndex == BoneTable[index.x] && hasWeights == 0)
                    weight += 1.0;

                return weight;
            }

            void main() {
            	proc_transform_matrix();
            	proc_calc_color();
            	proc_calc_texcoord0();
            	proc_calc_texcoord1();
            	proc_calc_texcoord2();
            	gl_Position = Position;

                float totalWeight = BoneWeightDisplay(ivec4(aBoneIndex));
                WeightPreview = vec4(BoneWeightColor(totalWeight).rgb, 1);
            }
            """
        );

    public static ShaderSource GetFragmentShader(string name, H3DMaterialParams materialParams)
    {
        FragmentShaderGenerator generator = new(materialParams);

        return new(name + ".frag", ShaderType.FragmentShader, generator.GetFragShader());
    }
}
