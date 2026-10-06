// TyreModel's marks on the device (TyreMarks.cs): how far a tyre has scrubbed toward writing, and how dark what it
// writes is. Line for line with the host, `precise` where it rounds twice.
//
// The including kernel includes tyre.glsl first, and its TYRE_FIGURES also carries markOnsetM and markPloughFloor.

const float TyreMarkSaturationSpan = 4.0;

// How far this tyre has dragged without letting go, capped at the onset (TyreModel.ScrubTravelM).
float tyreScrubTravelM(float carriedM, float slideSpeedMps, float dtS)
{
    float rateMps = slideSpeedMps > 0.0 ? slideSpeedMps : -TYRE_FIGURES.markSlipMps;
    precise float travelledM = carriedM + (rateMps * dtS);
    return clamp(travelledM, 0.0, TYRE_FIGURES.markOnsetM);
}

// How hard a mark the power leaves past the threshold the ground shrugs off, 0..1 (TyreModel.MarkIntensity).
float tyreMarkIntensity(float powerM2S3, float thresholdM2S3)
{
    if (thresholdM2S3 <= 0.0) return 0.0;

    precise float share = (powerM2S3 - thresholdM2S3) / (thresholdM2S3 * TyreMarkSaturationSpan);
    return clamp(share, 0.0, 1.0);
}

// How dark a mark this wheel leaves on the ground under it (TyreModel.GroundMarkIntensity).
float tyreGroundMarkIntensity(TyreSurface surface, TyreScrub scrub, float scrubTravelM)
{
    float slidePowerM2S3 = scrubTravelM >= TYRE_FIGURES.markOnsetM ? scrub.slidePowerM2S3 : 0.0;
    precise float powerM2S3 = slidePowerM2S3 + scrub.ploughPowerM2S3;
    float intensity = tyreMarkIntensity(powerM2S3, surface.markThresholdM2S3);
    return (scrub.flags & 1u) != 0u ? max(intensity, TYRE_FIGURES.markPloughFloor) : intensity;
}
