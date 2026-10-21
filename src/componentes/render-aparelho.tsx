import renderSe from "../recursos/imagens/dispositivos/iphone-se.svg";
import renderSingle from "../recursos/imagens/dispositivos/iphone-single.svg";
import renderVertical from "../recursos/imagens/dispositivos/iphone-vertical.svg";
import renderSquare from "../recursos/imagens/dispositivos/iphone-square.svg";
import renderDiagonal from "../recursos/imagens/dispositivos/iphone-diagonal.svg";
import renderIsland from "../recursos/imagens/dispositivos/iphone-island.svg";
import renderTablet from "../recursos/imagens/dispositivos/ipad.svg";
import { DEFAULT_ART, type PhoneArt } from "../utilitarios/modelos-dispositivo";

const RENDERS: Record<PhoneArt, string> = {
  se: renderSe,
  single: renderSingle,
  vertical: renderVertical,
  square: renderSquare,
  diagonal: renderDiagonal,
  island: renderIsland,
  tablet: renderTablet,
};

/**
 * The device visual.
 *
 * The artwork is a vector render shipped with the app, so it stays sharp at
 * any size. The one that is shown is the one of the connected phone, picked
 * by the camera layout of the model. The red halo is drawn separately in CSS
 * so it can breathe with the panel background instead of being baked into the
 * artwork.
 */
export const PhoneRender = ({
  art,
  className,
}: {
  art?: PhoneArt;
  className?: string;
}) => {
  return (
    <div className={`phone-render${className ? ` ${className}` : ""}`}>
      <span className="phone-halo" aria-hidden="true" />
      <img
        className="phone-image"
        src={RENDERS[art ?? DEFAULT_ART]}
        alt=""
        aria-hidden="true"
        draggable={false}
      />
    </div>
  );
};
